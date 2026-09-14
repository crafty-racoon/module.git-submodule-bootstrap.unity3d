using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace CraftyRacoon.GitSubmoduleBootstrap.Editor
{
    internal sealed class GitCommandRunner
    {
        internal GitCommandResult Run(string workingDirectory, params string[] arguments)
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = BuildArguments(arguments),
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                startInfo.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";
                startInfo.EnvironmentVariables["GCM_INTERACTIVE"] = "Never";

                using (Process process = new Process { StartInfo = startInfo })
                {
                    if (!process.Start())
                    {
                        return new GitCommandResult(-1, string.Empty, "Git failed to start.");
                    }

                    Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
                    Task<string> standardError = process.StandardError.ReadToEndAsync();
                    process.WaitForExit();
                    Task.WaitAll(standardOutput, standardError);
                    return new GitCommandResult(process.ExitCode, standardOutput.Result, standardError.Result);
                }
            }
            catch (Exception exception)
            {
                return new GitCommandResult(-1, string.Empty, exception.ToString());
            }
        }

        private static string BuildArguments(IEnumerable<string> arguments)
        {
            List<string> escapedArguments = new List<string>();
            foreach (string argument in arguments)
            {
                escapedArguments.Add(QuoteArgument(argument));
            }

            return string.Join(" ", escapedArguments);
        }

        private static string QuoteArgument(string argument)
        {
            if (argument.Length > 0 && argument.IndexOfAny(new[] { ' ', '\t', '\n', '\v', '"' }) < 0)
            {
                return argument;
            }

            StringBuilder builder = new StringBuilder();
            builder.Append('"');
            int backslashCount = 0;
            foreach (char character in argument)
            {
                if (character == '\\')
                {
                    backslashCount++;
                    continue;
                }

                if (character == '"')
                {
                    builder.Append('\\', backslashCount * 2 + 1);
                    builder.Append(character);
                    backslashCount = 0;
                    continue;
                }

                builder.Append('\\', backslashCount);
                builder.Append(character);
                backslashCount = 0;
            }

            builder.Append('\\', backslashCount * 2);
            builder.Append('"');
            return builder.ToString();
        }
    }

    internal sealed class GitCommandResult
    {
        internal GitCommandResult(int exitCode, string standardOutput, string standardError)
        {
            ExitCode = exitCode;
            StandardOutput = standardOutput;
            StandardError = standardError;
        }

        internal int ExitCode { get; }

        internal string StandardOutput { get; }

        internal string StandardError { get; }
    }
}
