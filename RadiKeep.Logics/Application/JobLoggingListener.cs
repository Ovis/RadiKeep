using Microsoft.Extensions.Logging;
using Quartz;
using ZLogger;

namespace RadiKeep.Logics.Application
{
    public class JobLoggingListener(ILogger<JobLoggingListener> logger) : IJobListener
    {
        private sealed class NoopScope : IDisposable
        {
            public static readonly NoopScope Instance = new();
            public void Dispose() { }
        }

        private IDisposable BeginJobScope(IJobExecutionContext context)
        {
            return logger.BeginScope(new Dictionary<string, object?>
            {
                ["JobKey"] = context.JobDetail.Key.ToString(),
                ["FireInstanceId"] = context.FireInstanceId ?? string.Empty
            }) ?? NoopScope.Instance;
        }

        public Task JobExecutionVetoed(IJobExecutionContext context, CancellationToken cancellationToken = new())
        {
            using var scope = BeginJobScope(context);
            logger.ZLogWarning($"Job {context.JobDetail.Key} was vetoed.");
            logger.ZLogWarning($"vetoed FireInstanceId:{context.FireInstanceId}");
            logger.ZLogWarning($"vetoed FireTimeUtc:{context.FireTimeUtc}");
            logger.ZLogWarning($"vetoed JobDetail:{context.JobDetail}");
            return Task.CompletedTask;
        }

        public string Name => "JobLoggingListener";

        public Task JobToBeExecuted(IJobExecutionContext context, CancellationToken cancellationToken = new())
        {
            using var scope = BeginJobScope(context);
            logger.ZLogDebug($"Job {context.JobDetail.Key} is about to be executed.");
            logger.ZLogDebug($"executed FireInstanceId:{context.FireInstanceId}");
            logger.ZLogDebug($"executed FireTimeUtc:{context.FireTimeUtc}");
            logger.ZLogDebug($"executed JobDetail:{context.JobDetail}");
            return Task.CompletedTask;
        }

        public Task JobWasExecuted(IJobExecutionContext context, JobExecutionException? jobException, CancellationToken cancellationToken = new())
        {
            using var scope = BeginJobScope(context);
            if (jobException == null)
            {
                logger.ZLogDebug($"Job {context.JobDetail.Key} was executed successfully.");
                logger.ZLogDebug($"successfully FireInstanceId:{context.FireInstanceId}");
                logger.ZLogDebug($"successfully FireTimeUtc:{context.FireTimeUtc}");
                logger.ZLogDebug($"successfully JobDetail:{context.JobDetail}");
            }
            else
            {
                logger.ZLogError($"Job {context.JobDetail.Key} failed: {jobException.Message}");
                logger.ZLogError($"failed FireInstanceId:{context.FireInstanceId}");
                logger.ZLogError($"failed FireTimeUtc:{context.FireTimeUtc}");
                logger.ZLogError($"failed JobDetail:{context.JobDetail}");
                logger.ZLogError($"Exception StackTrace:{jobException.StackTrace}");
                logger.ZLogError($"Exception Source:{jobException.Source}");
            }
            return Task.CompletedTask;
        }
    }
}
