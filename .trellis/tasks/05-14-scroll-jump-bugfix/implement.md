# Implementation Notes — v1.0.2 Bug Fix Pass

## Trigger

用户报告："阅读用滚轮滑动时有 bug，会滑着滑着突然滑到下面很多章。"

定位后追加完整代码审查，共修复 12 项 bug。

## Root Cause — 滚轮跳章

`MainWindow.xaml.cs` 的渐进式章节加载链路存在两个互相放大的缺陷：

1. **`RemoveFirstChapter` 用比例还原滚动位置**：删掉首章后用 `ScrollableHeight * prevScrollRatio` 还原，让用户停在「新内容的同一比例处」而不是「同一段视觉内容」。每触发一次 Trim 就向下跳约 1 章。
2. **`UpdateCurrentChapterFromScroll` 中 Append/Prepend 在 Trim 之前**：Append 改了 text 但 WPF 还没排版，Trim 读到的 `sv.ScrollableHeight` 是旧值、`Text.Length` 是新值，`currentLocalIdx` 算出来偏大 1，提前触发 Trim 又叠加上面的位置错误。

## Fix

- 调换顺序：`TrimOldChapters()` 在 `AppendNextChapter()` / `PrependPreviousChapter()` 之前，保证 Trim 在干净状态下运行。
- `RemoveFirstChapter` / `PrependPreviousChapter` 改用 `sv.UpdateLayout()` 强制同步排版 + 按 `prevScrollable - newScrollable` 的真实像素差还原。

## Audit Findings — 全面审查

按 `.trellis/spec/backend/*.md` 规范通读全部源码后定位的其它 bug，按严重程度：

### Critical / High
- **跨次启动跳章**：`ReadingProgress.ChapterScrollRatio` 命名误导——实际保存的是「整个加载窗口的相对位置」。读 chapter X 时缓冲窗口随 Trim/Append 偏移到 `[X-3..X+4]`，X 落在缓冲 6/8≈0.75；下次启动加载 `[X-3..X+3]`，X 落在 3/7≈0.43。把 0.75 应用到新窗口得到 chapter X+2 的位置。
  - 修复：新增 `ComputeChapterRelativeRatio()` 把 sv 位置换算到「当前章内的 ratio」，`ScrollToChapterRatio(idx, ratio)` 反向换算，保存/恢复全路径改用章内 ratio。
  - 顺带修复 `ReadingPercent`：原本 `currentChapterChars * chapterRatio` 把缓冲 ratio 当成章内 ratio 用，状态栏百分比一直不对。
- **OnlineBookStoreViewModel 事件不解绑**：`_downloadService` 是 app 生命周期单例，VM 订阅的三个事件在窗口关闭时未解绑，每打开一次书城窗口泄漏一个 VM。修复：VM 实现 `IDisposable`，`OnlineBookStoreWindow.Closed` 触发 Dispose。
- **DownloadService taskCts 泄漏**：finally 用 `TryRemove(out _)` 丢弃 cts。修复：保留出参并 `Dispose()`，`CancelDownload` 改用 `TryGetValue` 让 finally 统一释放。

### Medium
- **TxtParser 标题入正文**：`contentStart = startIndex + match.Length` 跳过的是正则匹配（"第一章"）而不是整个标题行（"第一章 奇怪的梦"），正文首行重复一次标题文字。修复：`contentStart = lineEnd + 1`。
- **ReadingPercent 空判断失效**：`_currentNovel?.Chapters.Count == 0` 在 null 时为 false，改显式 `is null ||`。
- **OpenFile 切书丢精确进度**：MainViewModel 没法直接读到 sv 位置，原来直接调 `LoadNovel` 走兜底保存（scrollOffset=0）。新增 `OnLoadNovelRequested` 事件让 MainWindow 先 `SaveCurrentProgress` 再 `LoadNovel`。
- **DownloadService / BookSourceService 非原子写**：直接 `File.WriteAllText`，崩溃会留下截断文件。把 `DataStore.WriteJsonAtomically` 拆出 `internal static WriteAtomically(path, content)`，两个 service 都改用。
- **DownloadService.RemoveTask 未加锁**：与其它 `_tasks` 读写不一致，补上 `lock (_tasksLock)`。
- **自动滚动到末页不停**：`OnScrollTick` 到底后 `LineDown` 无效，但定时器仍跑。检测到 `_loadedChapterEnd >= chapters.Count - 1` 且 `VerticalOffset >= ScrollableHeight` 时主动 `ToggleAutoScrollCommand.Execute(null)` 并 Toast。

### Low
- 删除 `MainViewModel.OnScrollTick` —— 转发事件无人订阅，闭包让 VM 在某些路径下被多挂一份引用。
- `MainWindow.RegisterHotkeys` 字典里两条 entry 挤在一行的排版。

## Spec Compliance

按 `.trellis/spec/backend/quality-guidelines.md` 自查：

- ✅ 命名：事件使用 `On` 前缀（`OnLoadNovelRequested`），私有字段 `_disposed`，方法 PascalCase
- ✅ 异常处理：`SaveTasks` 的 catch 块保留 `Debug.WriteLine`，`WriteAtomically` 直接抛错由 caller 处理（DownloadService 包了 catch，BookSourceService.Save 维持原行为不静默吞）
- ✅ 中文错误消息：`ShowToast("已到达末尾")` 沿用中文 UI
- ✅ 没有引入 DI / 测试 / 日志框架
- ✅ 没有 `#region`、没有 `Console.WriteLine`
- ⚠️ ViewModel 实现 IDisposable 是对 spec 第 48 行（"IDisposable on services...")的扩展，但在长生命周期单例事件订阅场景下没有更好的选择，等遇到第二个再考虑提炼成 spec

## Validation

- `dotnet build` 全绿，三轮（修滚轮 / 修原子写 / 全部完成后）
- grep 验证引用：`OnScrollTick` 只剩 `AutoScrollService` 源 + `MainWindow` 唯一消费者；`OnLoadNovelRequested` 声明 + 触发 + 订阅链路闭合；`WriteAtomically` 三处调用都成功；`ComputeChapterRelativeRatio` 三处调用一致
- 用户已手动验证：滚轮跳章、跨次启动停在同一段、自动滚到底停止、下载取消干净

## Files Changed

```
src/NovelMoyo/Services/BookSource/BookSourceService.cs       |  3 +-
src/NovelMoyo/Services/DataStore.cs                          | 18 ++-
src/NovelMoyo/Services/Download/DownloadService.cs           | 20 ++-
src/NovelMoyo/Services/NovelParser/TxtParser.cs              | 13 +-
src/NovelMoyo/ViewModels/MainViewModel.cs                    | 25 ++--
src/NovelMoyo/ViewModels/OnlineBookStoreViewModel.cs         | 15 +-
src/NovelMoyo/Views/MainWindow.xaml.cs                       | 155 ++++++++++++---
src/NovelMoyo/Views/OnlineBookStoreWindow.xaml.cs            |  1 +
```
