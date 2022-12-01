using CliWrap;
using CliWrap.Buffered;
using CliWrap.EventStream;
using ImGuiNET;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using SharpDX;
using SixLabors.ImageSharp.PixelFormats;
using strange.extensions.context.api;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Wanderer.Common;

namespace Wanderer.GitRepository.View
{
    public class GitCommandView : ImGuiView
    {
        private static GitCommandView s_gitCommandView;
        private IGitCommand m_gitCommand;
        private bool m_open;

        public GitCommandView(IContext context) : base(context)
        {
            s_gitCommandView = this;
        }

        public override void OnDraw()
        {
            if (m_gitCommand != null)
            {
                //var size = ImGui.GetWindowSize() * 0.3f;
                //var pos = ImGui.GetWindowSize() * 0.5f;
                var viewport = ImGui.GetMainViewport();
                ImGui.OpenPopup(m_gitCommand.Name);
                ImGui.SetNextWindowSize(viewport.WorkSize * 0.8f);
                //ImGui.SetNextWindowPos(pos);
                bool openCommandDraw = true;
                if (ImGui.BeginPopupModal(m_gitCommand.Name, ref m_open, ImGuiWindowFlags.NoResize))
                {
                    openCommandDraw = m_gitCommand.Draw();
                }

                openCommandDraw = openCommandDraw && m_open;
                if (!openCommandDraw)
                {
                    m_open = false;
                    m_gitCommand.Dispose();
                    m_gitCommand = null;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }
        }

        private void SetGitCommand<T>(params object[] pArgs)  where T : IGitCommand
        {
            if (m_gitCommand != null)
            {
                Log.Warn("Command {0} is runing.", m_gitCommand.Name);
                return;
            }
            m_open = true;
            m_gitCommand = (T)Activator.CreateInstance(typeof(T),pArgs);
        }

        public static void RunGitCommandView<T>(params object[] pArgs) where T : IGitCommand
        {
            if (s_gitCommandView != null)
            {
               s_gitCommandView.SetGitCommand<T>(pArgs);
            }
        }

        public static void ShowTerminal(string repoPath)
        {
            var gitbashPath = new ProcessGitCommand().WaitForExit("where","git-bash");
            if (!string.IsNullOrEmpty(gitbashPath) && gitbashPath.Contains("git-bash"))
            {
                Log.Info("where git-bash : {0}", gitbashPath);

                Process.Start(gitbashPath, $"--cd={Path.Combine(repoPath, "../")}");
            }
            else
            {
                Log.Info("下载Git https://github.com/git-for-windows/git/releases/download/v2.38.0.windows.1/MinGit-2.38.0-64-bit.zip");
            }
        }

    }

    public interface IGitCommand:IDisposable
    {
        string Name { get; }
        bool Draw();
        
    }

    public class PushGitCommand : ProcessGitCommand
    {
        public override  string Name => "Git Push";

        private List<string> m_lines = new List<string>();
        private CancellationTokenSource m_cancellationTokenSource;

        private bool m_exit;

        public PushGitCommand(string repoPath)
        {
            m_exit = false;
            m_cancellationTokenSource = new CancellationTokenSource();
            Exec(repoPath, m_cancellationTokenSource);
        }

        private void AddNewLine(string line)
        {
            if (!string.IsNullOrEmpty(line))
            {
                m_lines.Add(line);
                Log.Info(line);
            }
        }

        private async void Exec(string dir, CancellationTokenSource cts)
        {
            CancellationToken cancellationToken = default(CancellationToken);
            if (cts != null)
            {
                cancellationToken = cts.Token;
            }
            try
            {
                AddNewLine(dir);
                AddNewLine("$ git push");
                var result = await Cli.Wrap("git")
               .WithArguments("push")
               .WithWorkingDirectory(dir)
               .WithStandardOutputPipe(PipeTarget.ToDelegate((line) =>
               {
                   Log.Info(line);
                   m_lines.Add(line);
               }, Encoding.UTF8))
               .WithStandardErrorPipe(PipeTarget.ToDelegate((line) =>
               {
                   Log.Info(line);
                   m_lines.Add(line);
               }, Encoding.UTF8))
               .WithValidation(CommandResultValidation.None)
               .ExecuteAsync(cancellationToken);

                AddNewLine($"{result.ExitTime} ExitCode {result.ExitCode}");
            }
            catch (Exception e)
            {
                AddNewLine($"git push exception: {e}");
            }
            finally
            {
                if (m_cancellationTokenSource != null)
                {
                    m_cancellationTokenSource.Dispose();
                    m_cancellationTokenSource = null;
                }
                m_exit = true;
            }
            //m_runing = false;
        }

        public override bool Draw()
        {
            if (m_lines.Count > 0)
            {
                ImGui.BeginGroup();
                foreach (var item in m_lines)
                {
                    ImGui.Text(item);
                }
                ImGui.EndGroup();
            }

            if (!m_exit)
            {
                if (ImGui.Button("Cancel"))
                {
                    if (m_cancellationTokenSource != null)
                    {
                        m_cancellationTokenSource.Cancel(true);
                    }
                    m_exit = true;
                    return false;
                }
            }
            else
            {
                if (ImGui.Button("Close"))
                {
                    return false;
                }
            }

            return true;
        }
    }

    public class ProcessGitCommand : IGitCommand
    {
        public virtual string Name => "Process Command";
        public ProcessGitCommand()
        {
        
        }

        public virtual bool Draw()
        {
            return true;
        }

        public Process Run(string fileName, string arguments,string workDir=null)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            if (!string.IsNullOrEmpty(workDir))
            {
                startInfo.WorkingDirectory = workDir;
            }
            startInfo.FileName = fileName;
            startInfo.Arguments = arguments;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardInput = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;

            return Process.Start(startInfo);
        }

        public string WaitForExit(string fileName, string arguments, string workDir = null)
        {
            var process = Run(fileName, arguments, workDir);
            process.WaitForExit();
            string readLine = process.StandardOutput.ReadLine();
            process.Kill();
            process.Dispose();
            return readLine;
        }

      

        public virtual void Dispose()
        {
        }

    }

}
