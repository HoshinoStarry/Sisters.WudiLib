using System;
using System.IO;
using Karambolo.Extensions.Logging.File;
using Microsoft.Extensions.Logging;

namespace Sisters.WudiLib
{
    internal static class Utilities
    {
        /// <summary>
        /// 检查<see cref="string"/>是否为<c>null</c>或空值，并抛出相应的异常。
        /// </summary>
        /// <param name="argument">要检查的<see cref="string"/>。</param>
        /// <param name="paramName">TODO</param>
        /// <exception cref="ArgumentException"><c>argument</c>为空。</exception>
        /// <exception cref="ArgumentNullException"><c>argument</c>为<c>null</c>。</exception>
        internal static void CheckStringArgument(string argument, string paramName)
        {
            if (argument is null)
                throw new ArgumentNullException(paramName);
            if (argument.Length == 0)
                throw new ArgumentException($"{paramName}为空。", paramName);
        }
        
        public static ILogger GetLogger(string name)
        {
            return LoggerFactory.Create(builder =>
            {
                builder.AddSimpleConsole(options =>
                    {
                        options.IncludeScopes = true;
                        options.SingleLine = true;
                        options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
                    }
                );
                builder.AddFile(options =>
                {
                    options.RootPath = AppContext.BaseDirectory;
                    options.Files = [new LogFileOptions{ Path = $"logs/{name}_{DateTime.Now:yyyyMMddTHHmmss}.log" }];
                    options.IncludeScopes = true;
                });
            }).CreateLogger(name);
        }

        public static ILogger GetLogger(Type type) => GetLogger(type.FullName ?? type.Name);
        
        public static ILogger GetLogger<T>() => GetLogger(typeof(T));
    }
}
