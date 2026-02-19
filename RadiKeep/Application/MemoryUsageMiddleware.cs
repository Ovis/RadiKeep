using System.Diagnostics;
using ZLogger;

namespace RadiKeep.Application
{
    public class MemoryUsageMiddleware(RequestDelegate next, ILogger<MemoryUsageMiddleware> logger)
    {
        public async Task InvokeAsync(HttpContext context)
        {
            // 現在のメモリー使用量を取得
            var process = Process.GetCurrentProcess();
            var memoryUsageBefore = process.WorkingSet64;

            // 次のミドルウェアまたはコントローラアクションを呼び出す
            await next(context);

            // 現在のメモリー使用量を取得
            var memoryUsageAfter = process.WorkingSet64;

            // メモリー使用量をコンソールに出力
            logger.ZLogDebug($"Memory Usage Before: {memoryUsageBefore / 1024 / 1024} MB");
            logger.ZLogDebug($"Memory Usage After: {memoryUsageAfter / 1024 / 1024} MB");
        }
    }

    // ミドルウェアの登録
    public static class MemoryUsageMiddlewareExtensions
    {
        public static IApplicationBuilder UseMemoryUsageMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<MemoryUsageMiddleware>();
        }
    }
}