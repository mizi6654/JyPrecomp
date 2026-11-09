namespace JyPrecomp.Core;

/// <summary>
/// 预合成流水线。还原优先用「精确次数 Ctrl+Z」（与操作步数一致），
/// 磁盘备份作保险（崩溃/关草稿后仍可从文件恢复）。
/// </summary>
public class PrecompPipeline
{
    public string DraftFolder { get; set; } = "";
    public string? JianyingExe { get; set; }
    public int CompoundWaitSeconds { get; set; } = 5;
    public int PrecomposeTimeoutMinutes { get; set; } = 40;
    public int StableSeconds { get; set; } = 3;

    public string? LastBackupDir { get; private set; }
    public string? LastProductPath { get; private set; }

    /// <summary>本流水线产生的、可用 Ctrl+Z 撤销的步数。</summary>
    public int UndoSteps { get; private set; }

    public async Task<bool> RunAsync(Action<string> log, CancellationToken cancel)
    {
        UndoSteps = 0;
        if (string.IsNullOrWhiteSpace(DraftFolder) || !Directory.Exists(DraftFolder))
        {
            log("请先选择有效草稿目录");
            return false;
        }

        try
        {
            log("【0】备份工程文件（保险，防撤销栈丢失）...");
            LastBackupDir = DraftBackup.Backup(DraftFolder, null, log);
            var before = CombinationWatcher.SnapshotVideoFiles(DraftFolder);

            if (!JianyingWindow.IsRunning())
            {
                if (!JianyingWindow.StartJianying(JianyingExe, log)) return false;
                await Task.Delay(5000, cancel);
            }

            log("【1】置前剪映");
            if (!JianyingWindow.Activate(log)) return false;
            InputSender.ClickWindowCenter(JianyingWindow.FindMainWindow(), log);
            await Task.Delay(350, cancel);

            log("【2】Ctrl+A 全选");
            InputSender.SendSelectAll(log);
            await Task.Delay(350, cancel);

            log("【3】Alt+G 复合");
            InputSender.SendCreateCompound(log);
            UndoSteps++; // 复合
            await Task.Delay(CompoundWaitSeconds * 1000, cancel);

            log("【4】Ctrl+Alt+J 预合成");
            JianyingWindow.Activate(log);
            await Task.Delay(200, cancel);
            InputSender.SendPrecompose(log);
            UndoSteps++; // 预合成（多数版本占一步撤销）

            log("【5】等待 combination 成品...");
            LastProductPath = await CombinationWatcher.WaitNewStableVideoAsync(
                DraftFolder, before, StableSeconds, PrecomposeTimeoutMinutes, cancel, log);
            if (LastProductPath == null)
            {
                log("未得到成品");
                return false;
            }
            log($"成品: {Path.GetFileName(LastProductPath)}");

            // 保存尽量短等（导出前需要落盘，但不必双等很久）
            log("【6】保存");
            JianyingWindow.Activate(log);
            InputSender.SendSave(log);
            await Task.Delay(1200, cancel);

            log("【7】清空时间线");
            JianyingWindow.Activate(log);
            InputSender.ClickWindowCenter(JianyingWindow.FindMainWindow(), null);
            await Task.Delay(200, cancel);
            InputSender.SendSelectAll(log);
            await Task.Delay(250, cancel);
            InputSender.SendDelete(log);
            UndoSteps++; // 删除
            await Task.Delay(300, cancel);
            InputSender.SendHome(log);

            log("【8】videocut:// 导入成品");
            JianyingWindow.Activate(log);
            await Task.Delay(200, cancel);
            FileImportHelper.ImportViaVideocutProtocol(LastProductPath, log, newDraft: false);
            UndoSteps++; // 导入
            // 导入后稍等剪映吃进素材即可，不必死等 10 秒
            await Task.Delay(800, cancel);

            log("【9】Ctrl+E 导出");
            JianyingWindow.Activate(log);
            await Task.Delay(200, cancel);
            InputSender.SendExport(log);

            log("可导出。完成后点「还原草稿」（将 Ctrl+Z × 5）。");
            log($"备份仍保留: {LastBackupDir}");
            return true;
        }
        catch (OperationCanceledException)
        {
            log("已中止");
            return false;
        }
        catch (Exception ex)
        {
            log("异常: " + ex.Message);
            return false;
        }
    }

    public void Restore(Action<string> log)
    {
        // 实测：复合 + 预合成 + 清空 + 导入 在剪映撤销栈里常为 5 步
        // （4 步会停在「恢复预合成处理中」）。原软件符号是 RestoreOriginalDraft + keybd_event，
        // 无公开 undo 次数；此处以你本机验证为准。
        int steps = 5;

        log($"还原：Ctrl+Z × {steps}…");
        if (!JianyingWindow.Activate(log))
        {
            log("无法置前剪映");
            return;
        }
        Thread.Sleep(250);
        for (int i = 1; i <= steps; i++)
        {
            log($"  撤销 {i}/{steps}");
            InputSender.SendUndo(null);
            Thread.Sleep(300);
        }
        log("界面撤销完成。");

        // 磁盘保险：静默写回，不依赖热加载；下次打开草稿也一致
        if (!string.IsNullOrEmpty(LastBackupDir) && Directory.Exists(LastBackupDir))
        {
            log("同步写回磁盘备份（保险，不依赖界面刷新）…");
            try
            {
                DraftBackup.Restore(LastBackupDir, DraftFolder, msg =>
                {
                    // 压缩 DraftBackup 的啰嗦提示
                    if (msg.Contains("已写回") || msg.Contains("失败"))
                        log(msg);
                });
            }
            catch (Exception ex)
            {
                log("磁盘写回: " + ex.Message);
            }
        }

        log("还原结束。若多撤了一步可用 Ctrl+Y；素材箱残留可手动删。");
    }
}
