using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using RadiKeep.Logics.Domain.Station;
using RadiKeep.Logics.Infrastructure.Station;
using RadiKeep.Logics.Interfaces;
using RadiKeep.Logics.Logics.RadikoLogic;
using RadiKeep.Logics.Logics.StationLogic;
using RadiKeep.Logics.Mappers;
using RadiKeep.Logics.Models.NhkRadiru;
using RadiKeep.Logics.Primitives;
using RadiKeep.Logics.Primitives.DataAnnotations;
using RadiKeep.Logics.RdbContext;
using RadiKeep.Logics.Services;
using RadiKeep.Logics.Tests.Mocks;

namespace RadiKeep.Logics.Tests.LogicTest
{
    public class StationLobLogicTests : UnitTestBase
    {
        private Mock<ILogger<StationLobLogic>> _loggerMock;
        private Mock<IAppConfigurationService> _configServiceMock;
        private Mock<IRadikoApiClient> _radikoApiClientMock;
        private Mock<IHttpClientFactory> _httpClientFactoryMock;
        private IEntryMapper _entryMapper;
        private RadioDbContext _dbContext;
        private StationLobLogic _stationLogic;
        private Mock<RadikoUniqueProcessLogic> _radikoUniqueProcessLogicMock;
        private IStationRepository _stationRepository;

        [SetUp]
        public async Task Setup()
        {
            _loggerMock = new Mock<ILogger<StationLobLogic>>();
            _configServiceMock = new Mock<IAppConfigurationService>();
            _radikoApiClientMock = new Mock<IRadikoApiClient>();
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _httpClientFactoryMock
                .Setup(x => x.CreateClient(It.IsAny<string>()))
                .Returns(new HttpClient(new HttpClientHandler()));
            _dbContext = DbContext;
            _dbContext.ChangeTracker.Clear();
            await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM RadikoStations");
            await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM NhkRadiruStations");
            _entryMapper = new EntryMapper(_configServiceMock.Object);
            _stationRepository = new StationRepository(_dbContext);

            _radikoUniqueProcessLogicMock = new Mock<RadikoUniqueProcessLogic>(
                new Mock<ILogger<RadikoUniqueProcessLogic>>().Object,
                _configServiceMock.Object,
                new FakeHttpClientFactory(new HttpClient(new FakeHttpMessageHandler()))
            );

            _stationLogic = new StationLobLogic(
                _loggerMock.Object,
                _configServiceMock.Object,
                _radikoApiClientMock.Object,
                _stationRepository,
                _radikoUniqueProcessLogicMock.Object,
                _httpClientFactoryMock.Object,
                _entryMapper
            );
        }

        [Test]
        public async Task CheckInitializedRadikoStationAsync_未初期化状態テスト()
        {
            await using var dbTran = await _dbContext.Database.BeginTransactionAsync();

            try
            {
                await DbContext.Database.ExecuteSqlRawAsync("DELETE FROM RadikoStations");

                await dbTran.CommitAsync();
            }
            catch (Exception e)
            {
                await dbTran.RollbackAsync();
                Assert.Fail(e.Message);
            }

            var result = await _stationLogic.CheckInitializedRadikoStationAsync();

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public async Task CheckInitializedRadikoStationAsync_初期化状態テスト()
        {
            await using var dbTran = await _dbContext.Database.BeginTransactionAsync();

            try
            {
                await DbContext.Database.ExecuteSqlRawAsync("DELETE FROM RadikoStations");

                var stationEntry = new RadikoStation
                {
                    StationId = "TBS",
                    RegionId = "JP13"
                };

                _dbContext.RadikoStations.Add(stationEntry);
                await _dbContext.SaveChangesAsync();

                await dbTran.CommitAsync();
            }
            catch (Exception e)
            {
                await dbTran.RollbackAsync();
                Assert.Fail(e.Message);
            }

            var result = await _stationLogic.CheckInitializedRadikoStationAsync();

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public async Task UpsertRadikoStationDefinitionAsync_放送局リスト取得テスト()
        {
            _radikoApiClientMock
                .Setup(x => x.GetRadikoStationsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<RadikoStation>
                {
                    new() { StationId = "TBS", RegionId = "JP13", RegionName = "関東", StationName = "TBS" }
                });

            try
            {
                await _stationLogic.UpsertRadikoStationDefinitionAsync();
            }
            catch (Exception e)
            {
                Assert.Fail(e.Message);
            }

            Assert.Pass();
        }

        [Test]
        public async Task GetAllRadikoStationAsync_ShouldReturnListOfRadikoStation()
        {
            await using var dbTran = await _dbContext.Database.BeginTransactionAsync();

            try
            {
                await DbContext.Database.ExecuteSqlRawAsync("DELETE FROM RadikoStations");

                List<RadikoStation> stations =
                [
                    new()
                    {
                        StationId = "RN1",
                        RegionId = "JP10"
                    },

                    new()
                    {
                        StationId = "JOAK-FM",
                        RegionId = "JP14"
                    }
                ];

                _dbContext.RadikoStations.AddRange(stations);
                await _dbContext.SaveChangesAsync();

                await dbTran.CommitAsync();
            }
            catch (Exception e)
            {
                await dbTran.RollbackAsync();
                Assert.Fail(e.Message);
            }

            var result = await _stationLogic.GetAllRadikoStationAsync();

            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result.First(r => r.StationId == "RN1").RegionId, Is.EqualTo("JP10"));
        }

        [Test]
        public void GetRadiruStationAsync_一覧取得()
        {
            var list = _stationLogic.GetRadiruStationAsync().ToList();

            var areaCount = Enum.GetValues<RadiruAreaKind>().Length;
            var stationCount = Enumeration.GetAll<RadiruStationKind>().Count();

            Assert.That(list.Count, Is.EqualTo(areaCount * stationCount));
            Assert.That(list.Any(x => x.AreaId == RadiruAreaKind.東京.GetEnumCodeId()), Is.True);
        }

        [Test]
        public async Task GetNhkRadiruStationInformationByAreaAsync_取得できる()
        {
            await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM NhkRadiruStations");

            var areaId = RadiruAreaKind.東京.GetEnumCodeId();
            _dbContext.NhkRadiruStations.Add(new NhkRadiruStation
            {
                AreaId = areaId,
                AreaJpName = "東京",
                ApiKey = areaId,
                R1Hls = "r1",
                R2Hls = "r2",
                FmHls = "fm",
                ProgramNowOnAirApiUrl = "http://example",
                ProgramDetailApiUrlTemplate = "http://example/detail",
                DailyProgramApiUrlTemplate = "http://example/daily"
            });
            await _dbContext.SaveChangesAsync();

            var station = await _stationLogic.GetNhkRadiruStationInformationByAreaAsync(RadiruAreaKind.東京);

            Assert.That(station.AreaId, Is.EqualTo(areaId));
            Assert.That(station.AreaJpName, Is.EqualTo("東京"));
        }

        [TearDown]
        public async Task TearDown()
        {
            _dbContext.ChangeTracker.Clear();
            await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM RadikoStations");
            await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM NhkRadiruStations");
        }
    }
}
