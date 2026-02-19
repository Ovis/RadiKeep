using System.Net;
using Microsoft.Extensions.Logging;
using Moq;
using RadiKeep.Logics.Logics.RadikoLogic;
using RadiKeep.Logics.Services;
using RadiKeep.Logics.Tests.Mocks;

namespace RadiKeep.Logics.Tests.LogicTest
{
    [TestFixture]
    public class RadikoUniqueProcessLogicTests
    {
        private Mock<ILogger<RadikoUniqueProcessLogic>> _loggerMock;
        private Mock<IAppConfigurationService> _configMock;
        private FakeHttpMessageHandler _httpHandler;

        private RadikoUniqueProcessLogic _radikoLogic;

        [SetUp]
        public void Setup()
        {
            _loggerMock = new Mock<ILogger<RadikoUniqueProcessLogic>>();
            _configMock = new Mock<IAppConfigurationService>();
            _httpHandler = new FakeHttpMessageHandler();
            var httpClient = new HttpClient(_httpHandler);
            var httpClientFactory = new FakeHttpClientFactory(httpClient);
            _radikoLogic = new RadikoUniqueProcessLogic(_loggerMock.Object, _configMock.Object, httpClientFactory);
        }

        [TearDown]
        public void TearDown()
        {
            _httpHandler.Dispose();
        }


        [Test]
        public async Task GetRadikoAreaAsync_ValidResponse_エリア判定処理テスト()
        {
            _httpHandler.AddHandler(
                req => req.RequestUri!.ToString().Contains("http://radiko.jp/area/"),
                _ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(@"document.write('<span class=""JP13"">TOKYO JAPAN</span>');")
                });

            var result = await _radikoLogic.GetRadikoAreaAsync();

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Area, Is.EqualTo("JP13"));
        }

        [Test]
        public async Task GetPartialKeyString_キー取得テスト()
        {
            _httpHandler.AddHandler(
                req => req.RequestUri!.ToString().Contains("playerCommon.js"),
                _ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("new RadikoJSPlayer('a','b','bcd151073c03b352e1ef2fd66c32209da9ca0afa',{")
                });

            var result = await _radikoLogic.GetPartialKeyString();

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Key, Is.Not.Null);
            Assert.That(result.Key, Is.EqualTo("bcd151073c03b352e1ef2fd66c32209da9ca0afa"));
        }

        [Test]
        public async Task GetPartialKeyString_取得失敗()
        {
            _httpHandler.AddHandler(
                req => req.RequestUri!.ToString().Contains("playerCommon.js"),
                _ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("invalid")
                });

            var result = await _radikoLogic.GetPartialKeyString();

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Key, Is.EqualTo(string.Empty));
        }

        [Test]
        public async Task AuthorizeRadikoAsync_ヘッダー不足_失敗()
        {
            var handler = new FakeHttpMessageHandler();
            handler.AddHandler(
                req => req.RequestUri!.ToString().Contains("auth1"),
                _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(string.Empty) });
            var logic = new RadikoUniqueProcessLogic(
                _loggerMock.Object,
                _configMock.Object,
                new FakeHttpClientFactory(new HttpClient(handler)));

            var result = await logic.AuthorizeRadikoAsync("session");

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Token, Is.EqualTo(string.Empty));
            Assert.That(result.AreaId, Is.EqualTo(string.Empty));
        }

        [Test]
        public async Task AuthorizeRadikoAsync_Auth2Out_失敗()
        {
            var handler = new FakeHttpMessageHandler();
            handler.AddHandler(
                req => req.RequestUri!.ToString().Contains("auth1"),
                _ =>
                {
                    var response = new HttpResponseMessage(HttpStatusCode.OK);
                    response.Headers.Add("X-Radiko-AuthToken", "token");
                    response.Headers.Add("X-Radiko-KeyLength", "5");
                    response.Headers.Add("X-Radiko-KeyOffset", "0");
                    return response;
                });

            handler.AddHandler(
                req => req.RequestUri!.ToString().Contains("playerCommon.js"),
                _ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("new RadikoJSPlayer('a','b','abcde',{")
                });

            handler.AddHandler(
                req => req.RequestUri!.ToString().Contains("auth2"),
                _ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("OUT")
                });

            var logic = new RadikoUniqueProcessLogic(
                _loggerMock.Object,
                _configMock.Object,
                new FakeHttpClientFactory(new HttpClient(handler)));

            var result = await logic.AuthorizeRadikoAsync("session");

            Assert.That(result.IsSuccess, Is.False);
        }
    }
}

