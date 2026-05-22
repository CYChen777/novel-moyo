# Implementation Notes — v1.0.3 Pixel Offset Bug Fix Pass

## Root Cause — 方案1 字符→像素线性假设根本缺陷

v1.0.2 的修复仍基于 `charOffset / totalChars * scrollableHeight` 线性映射，假设每个字符占据相同像素高度。章节分隔符 `\n\n————————————————————\n\n` 只有24字符但渲染为约6行视觉高度（~96px），线性映射将其估算为 ~0.5 行。这导致：

1. 分隔符附近的像素定位严重偏移
2. 每章累积误差，在 Trim 边界时一次释放
3. 快速连续滚轮时误差叠加，数章跳跃

## Solution — 方案2 实测像素偏移

### 核心思路

用临时 `TextBlock`（相同字体/字号/宽度/行高）逐一实测每章的渲染高度，存储于 `_chapterPixelOffsets[]` 数组，平行 `_chapterCharOffsets[]`。所有章节↔滚动位置换算改用像素偏移。

### 新增/修改

- **`MeasureChapterHeights()`** — 用临时 TextBlock 逐一测量每章渲染高度，累计存入 `_chapterPixelOffsets`。窗口未显示时回退字符比例
- **`AppendNextChapter()`** — 预测量新章高度后直接追加到 `_chapterPixelOffsets`，不再依赖 `UpdateLayout` + `ScrollableHeight` 差值
- **`PrependPreviousChapter()`** — 预测量章节高度后移位 `_chapterPixelOffsets`，消除 ScrollableHeight 差值误差
- **`RemoveFirstChapter()`** — 使用 `_chapterPixelOffsets[1]` 实测值而非 `prevScrollable - newScrollable`
- **`TrimOldChapters()`** — 改用像素偏移检测应清理的章节
- **`UpdateCurrentChapterFromScroll()`** — 使用像素偏移识别当前章节
- **`ComputeChapterRelativeRatio()`** — 主要路径用像素偏移，回退字符比例
- **`ScrollToChapterRatio()`** — 主要路径用像素偏移，回退字符比例
- **`_needsDeferredScrollUpdate`** — 文本修改后异步 ScrollChanged 保护标志，防止布局未同步时重复触发 Trim/Append
- **`SizeChanged` 事件** — 窗口大小变化时重测像素偏移

### 其它修复 (共 17 项)

**Critical (4)** — 滚轮跳章根本修复 (如上)
**High (4)**
- 2 秒冷却期不够导致渐进加载干扰初始恢复 — Append/Prepend 末尾重置 `_contentLoadTime`
- 快捷键切章 + ScrollChanged 竞争 — MeasureChapterHeights 在 ScrollToChapterRatio 前确保已调用
- `_pendingScrollRatio` 未消费时 `CompositionTarget.Rendering` 事件泄漏 — OnClosing 清理
- nearBottom/nearTop 阈值在短章节时太敏感 — 增加 500px 像素绝对值阈值

**Medium (3)**
- ParagraphSpacing 变更时 `BuildFullBookContent` 丢失滚动位置 — 重建前后保存/恢复 VerticalOffset
- `HandleMouseWheel` 高 delta 鼠标放大过度 — `Math.Clamp(delta, -1000, 1000)`
- `LoadNovel` 切书时 scrollOffset=0 写入进度 — 移除冗余 `SaveCurrentProgressWithScroll(0,...)`

**Low (4)**
- `ChapterSeparator static readonly` → `const`
- 鼠标钩子可能重复安装 — InstallMouseHook 增加 `if (_mouseHookHandle != IntPtr.Zero) return;`
- 窗口隐藏时不保存进度 — OnClosing 隐藏分支新增 `SaveCurrentProgress`
- `ApplyParagraphSpacing` 小值无效 — `Round(spacing/4)=0` 修复为 `Math.Max(1, spacing/2)`

## Validation

- `dotnet build` — 0 warnings, 0 errors
- 像素偏移实测路径 + 字符比例回退路径均覆盖
- 无回归：所有旧功能保留前向后兼容
