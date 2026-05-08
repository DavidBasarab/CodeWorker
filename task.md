Running a task with the claude headless is is not working correctly.  We are going to focus on just running the claude tasks only.  Create a sample task ro tasks in this folder C:\Code\CodeWorker\CodeWorker.Tests\Tasks

And a way to the application to pass a task file and have the application run it with what would be a good output to guess what it should be like.  

Then start several iterations up to 6 and make it work as expect.  If you cannot get it working after 6 then let me know and we will think about another path.

  C:\Code\CodeWorker   FirstTask ≡  ?1  ﮫ⠀4.381s
 dotnet run --project .\CodeWorker\CodeWorker.csproj -- track
2026-05-06 22:08:09:399 | SystemScope.cs @ 53 Initialize |     Using assembly Testably.Abstractions.FileSystem.Interface, Version=10.0.0.0, Culture=neutral, PublicKeyToken=f24346c8579fcb48
2026-05-06 22:08:09:412 | SystemScope.cs @ 53 Initialize |     Using assembly FatCatCodeWorker, Version=1.0.0.0, Culture=neutral, PublicKeyToken=573280377c546557
2026-05-06 22:08:09:412 | SystemScope.cs @ 53 Initialize |     Using assembly FatCat.Toolkit, Version=1.0.335.0, Culture=neutral, PublicKeyToken=1916db74250bf654
2026-05-06 22:08:09:475 | SystemScope.cs @ 60 Initialize | Setting lifetime scope
2026.05.06 22:08:09:550 [INF] Welcome to Code Worker
2026.05.06 22:08:09:564 [INF] Running track for repository at "C:\Code\CodeWorker"
2026.05.06 22:08:09:565 [INF] Loading app settings from "C:\Code\CodeWorker\CodeWorker\bin\Debug\net10.0\appsettings.json"
2026.05.06 22:08:09:588 [INF] Tracking repository "C:\Code\CodeWorker"
2026.05.06 22:08:09:589 [INF] Saving app settings to "C:\Code\CodeWorker\CodeWorker\bin\Debug\net10.0\appsettings.json"

 Wed May 6 10:08:10 PM⠀
  C:\Code\CodeWorker   FirstTask ≡  ?1  ﮫ⠀2.521s
 dotnet run --project .\CodeWorker\CodeWorker.csproj -- list
2026-05-06 22:08:13:095 | SystemScope.cs @ 53 Initialize |     Using assembly Testably.Abstractions.FileSystem.Interface, Version=10.0.0.0, Culture=neutral, PublicKeyToken=f24346c8579fcb48
2026-05-06 22:08:13:111 | SystemScope.cs @ 53 Initialize |     Using assembly FatCatCodeWorker, Version=1.0.0.0, Culture=neutral, PublicKeyToken=573280377c546557
2026-05-06 22:08:13:111 | SystemScope.cs @ 53 Initialize |     Using assembly FatCat.Toolkit, Version=1.0.335.0, Culture=neutral, PublicKeyToken=1916db74250bf654
2026-05-06 22:08:13:176 | SystemScope.cs @ 60 Initialize | Setting lifetime scope
2026.05.06 22:08:13:249 [INF] Welcome to Code Worker
2026.05.06 22:08:13:262 [INF] Loading app settings from "C:\Code\CodeWorker\CodeWorker\bin\Debug\net10.0\appsettings.json"
2026.05.06 22:08:13:285 [INF] Tracked repositories (1):
2026.05.06 22:08:13:287 [INF]   "C:\Code\CodeWorker" (Enabled: True)

 Wed May 6 10:08:14 PM⠀
  C:\Code\CodeWorker   FirstTask ≡  ?1  ﮫ⠀2.427s
 dotnet run --project .\CodeWorker\CodeWorker.csproj --
2026-05-06 22:08:25:117 | SystemScope.cs @ 53 Initialize |     Using assembly Testably.Abstractions.FileSystem.Interface, Version=10.0.0.0, Culture=neutral, PublicKeyToken=f24346c8579fcb48
2026-05-06 22:08:25:130 | SystemScope.cs @ 53 Initialize |     Using assembly FatCatCodeWorker, Version=1.0.0.0, Culture=neutral, PublicKeyToken=573280377c546557
2026-05-06 22:08:25:130 | SystemScope.cs @ 53 Initialize |     Using assembly FatCat.Toolkit, Version=1.0.335.0, Culture=neutral, PublicKeyToken=1916db74250bf654
2026-05-06 22:08:25:196 | SystemScope.cs @ 60 Initialize | Setting lifetime scope
2026.05.06 22:08:25:267 [INF] Welcome to Code Worker
2026.05.06 22:08:25:278 [INF] Starting task runner
2026.05.06 22:08:25:279 [INF] Collecting Claude environment diagnostics
2026.05.06 22:08:25:282 [INF] Starting process "claude" in null
2026.05.06 22:08:25:289 [INF] Process started, PID=60796
2026.05.06 22:08:25:387 [INF] ["stdout"] "2.1.126 (Claude Code)
"
2026.05.06 22:08:25:407 [INF] Process exited. ExitCode=0, TimedOut=False, stdoutBytes=22, stderrBytes=0
2026.05.06 22:08:25:408 [INF] claude "--version" — ExitCode=0, Output="2.1.126 (Claude Code)"
2026.05.06 22:08:25:408 [INF] Loading app settings from "C:\Code\CodeWorker\CodeWorker\bin\Debug\net10.0\appsettings.json"
2026.05.06 22:08:25:430 [INF] Found 1 repository(ies) to process
2026.05.06 22:08:25:431 [INF] Processing repository "C:\Code\CodeWorker"
2026.05.06 22:08:25:434 [INF] Loading repository settings from "C:\Code\CodeWorker\tasks\settings.json"
2026.05.06 22:08:25:438 [INF] Found 1 reference file(s) in "C:\Code\CodeWorker\tasks/reference"
2026.05.06 22:08:25:438 [INF] Including 1 reference file(s): ".gitkeep"
2026.05.06 22:08:25:439 [INF] Claude settings: Model="claude-opus-4-7", MaxTurns=100, SkipPermissions=True, OutputFormat="json", TimeoutMinutes=90
2026.05.06 22:08:25:440 [INF] Discovering tasks in "C:\Code\CodeWorker\tasks/todo"
2026.05.06 22:08:25:441 [INF] Found 1 tasks in "C:\Code\CodeWorker\tasks/todo"
2026.05.06 22:08:25:442 [INF] Starting task "09-task-template-readme.md"
2026.05.06 22:08:25:442 [INF] Moving task "09-task-template-readme.md" to pending
2026.05.06 22:08:25:442 [INF] Moving task "09-task-template-readme.md" to "C:\Code\CodeWorker\tasks/pending"
2026.05.06 22:08:25:443 [INF] Task "09-task-template-readme.md" moved to pending
2026.05.06 22:08:25:443 [INF] Invoking Claude for "09-task-template-readme.md"
2026.05.06 22:08:25:444 [INF] Starting Claude with markdown file "C:\Code\CodeWorker\tasks/pending\09-task-template-readme.md"
2026.05.06 22:08:25:444 [INF] Claude settings: Model="claude-opus-4-7", MaxTurns=100, SkipPermissions=True, OutputFormat="json", TimeoutMinutes=90
2026.05.06 22:08:25:450 [INF] Launching detached pwsh wrapper Script="C:\Users\dbasa\AppData\Local\Temp\CodeWorker\Scripts\Run-ClaudeTask.ps1" Transcript="C:\Code\CodeWorker\tasks\pending\09-task-template-readme.transcript.jsonl"
2026.05.06 22:08:25:507 [INF] Detached wrapper started PID=63004
2026.05.06 22:08:25:509 [INF] Starting transcript tailer for "09-task-template-readme.md" TranscriptPath="C:\Code\CodeWorker\tasks\pending\09-task-template-readme.transcript.jsonl" PollInterval=00:00:00.2500000 IdleTimeout=00:10:00 WallClock=01:30:00
2026.05.06 22:08:55:648 [INF] Tailer heartbeat for "09-task-template-readme.md" — events=0, assistant=0, toolUse=0, toolResult=0, lastEventAgo=00:00:30.1402059
2026.05.06 22:09:25:754 [INF] Tailer heartbeat for "09-task-template-readme.md" — events=0, assistant=0, toolUse=0, toolResult=0, lastEventAgo=00:01:00.2457438
2026.05.06 22:09:55:934 [INF] Tailer heartbeat for "09-task-template-readme.md" — events=0, assistant=0, toolUse=0, toolResult=0, lastEventAgo=00:01:30.4259110
2026.05.06 22:10:25:959 [INF] Tailer heartbeat for "09-task-template-readme.md" — events=0, assistant=0, toolUse=0, toolResult=0, lastEventAgo=00:02:00.4507417
2026.05.06 22:10:56:095 [INF] Tailer heartbeat for "09-task-template-readme.md" — events=0, assistant=0, toolUse=0, toolResult=0, lastEventAgo=00:02:30.5865191
2026.05.06 22:11:26:133 [INF] Tailer heartbeat for "09-task-template-readme.md" — events=0, assistant=0, toolUse=0, toolResult=0, lastEventAgo=00:03:00.6243940
2026.05.06 22:11:56:380 [INF] Tailer heartbeat for "09-task-template-readme.md" — events=0, assistant=0, toolUse=0, toolResult=0, lastEventAgo=00:03:30.8723127
2026.05.06 22:12:26:467 [INF] Tailer heartbeat for "09-task-template-readme.md" — events=0, assistant=0, toolUse=0, toolResult=0, lastEventAgo=00:04:00.9593771
2026.05.06 22:12:56:663 [INF] Tailer heartbeat for "09-task-template-readme.md" — events=0, assistant=0, toolUse=0, toolResult=0, lastEventAgo=00:04:31.1547795
2026.05.06 22:13:26:808 [INF] Tailer heartbeat for "09-task-template-readme.md" — events=0, assistant=0, toolUse=0, toolResult=0, lastEventAgo=00:05:01.2997117
2026.05.06 22:13:56:907 [INF] Tailer heartbeat for "09-task-template-readme.md" — events=0, assistant=0, toolUse=0, toolResult=0, lastEventAgo=00:05:31.3989374
2026.05.06 22:14:26:920 [INF] Tailer heartbeat for "09-task-template-readme.md" — events=0, assistant=0, toolUse=0, toolResult=0, lastEventAgo=00:06:01.4118593
2026.05.06 22:14:57:181 [INF] Tailer heartbeat for "09-task-template-readme.md" — events=0, assistant=0, toolUse=0, toolResult=0, lastEventAgo=00:06:31.6733060
2026.05.06 22:15:27:204 [INF] Tailer heartbeat for "09-task-template-readme.md" — events=0, assistant=0, toolUse=0, toolResult=0, lastEventAgo=00:07:01.6959695
2026.05.06 22:15:57:437 [INF] Tailer heartbeat for "09-task-template-readme.md" — events=0, assistant=0, toolUse=0, toolResult=0, lastEventAgo=00:07:31.9292352
2026.05.06 22:16:27:627 [INF] Tailer heartbeat for "09-task-template-readme.md" — events=0, assistant=0, toolUse=0, toolResult=0, lastEventAgo=00:08:02.1190460
2026.05.06 22:16:57:676 [INF] Tailer heartbeat for "09-task-template-readme.md" — events=0, assistant=0, toolUse=0, toolResult=0, lastEventAgo=00:08:32.1679940
2026.05.06 22:17:27:939 [INF] Tailer heartbeat for "09-task-template-readme.md" — events=0, assistant=0, toolUse=0, toolResult=0, lastEventAgo=00:09:02.4308513
2026.05.06 22:17:58:200 [INF] Tailer heartbeat for "09-task-template-readme.md" — events=0, assistant=0, toolUse=0, toolResult=0, lastEventAgo=00:09:32.6922989
2026.05.06 22:18:25:643 [ERR] Tailer idle timeout for "09-task-template-readme.md" — no event in 00:10:00
2026.05.06 22:18:25:645 [INF] Claude tail finished StopReason=IdleTimeout ExitCode=-1 Events=0
2026.05.06 22:18:25:646 [WRN] Claude exited with non-zero exit code -1
2026.05.06 22:18:25:646 [INF] Claude exited with code -1
2026.05.06 22:18:25:647 [INF] Claude run returned for "09-task-template-readme.md": ExitCode=-1, TimedOut=True, FailedToStart=False, OutputLines=0, ErrorLines=0
2026.05.06 22:18:25:648 [INF] Logging result for task "09-task-template-readme.md" to "C:\Code\CodeWorker\CodeWorker.log"
2026.05.06 22:18:25:650 [INF] Classified task "09-task-template-readme.md" as Failed
2026.05.06 22:18:25:651 [INF] Writing task log for "09-task-template-readme.md" to "C:\Code\CodeWorker\tasks/logs\09-task-template-readme.log"
2026.05.06 22:18:25:667 [INF] Recorded run history for "09-task-template-readme.md"
2026.05.06 22:18:25:667 [INF] Invoking outcome handler for Failed on "09-task-template-readme.md"
2026.05.06 22:18:25:668 [INF] Handling Failed outcome for "09-task-template-readme.md": moving to "C:\Code\CodeWorker\tasks/failed"
2026.05.06 22:18:25:668 [INF] Moving task "09-task-template-readme.md" to "C:\Code\CodeWorker\tasks/failed"
2026.05.06 22:18:25:669 [INF] Moved "09-task-template-readme.md" to "C:\Code\CodeWorker\tasks/failed"
2026.05.06 22:18:25:669 [INF] Generating failed explanation for task "09-task-template-readme.md" at "C:\Code\CodeWorker\tasks/failed\09-task-template-readme.failed.md"
2026.05.06 22:18:25:670 [WRN] Task "09-task-template-readme.md" failed and StopOnFailed is enabled, stopping repository processing
2026.05.06 22:18:25:671 [INF] Outcome handler complete for "09-task-template-readme.md"
2026.05.06 22:18:25:672 [INF] Task runner complete — processed 1 repositories in 600.2637843s

 Wed May 6 10:18:26 PM⠀
  C:\Code\CodeWorker   FirstTask ≡  ?2 ~2  ﮫ⠀10m 2.795s
 git status
On branch FirstTask
Your branch is up to date with 'origin/FirstTask'.

Changes not staged for commit:
  (use "git add <file>..." to update what will be committed)
  (use "git restore <file>..." to discard changes in working directory)
        modified:   CodeWorker.log
        modified:   tasks/run-history.jsonl

Untracked files:
  (use "git add <file>..." to include in what will be committed)
        tasks/failed/09-task-template-readme.failed.md
        tasks/failed/09-task-template-readme.md

no changes added to commit (use "git add" and/or "git commit -a")

 Wed May 6 10:18:41 PM⠀
  C:\Code\CodeWorker   FirstTask ≡  ?2 ~2  ﮫ⠀37ms