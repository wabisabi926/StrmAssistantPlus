# 后续计划 / Roadmap

本文件随仓库同步，换机器 `git clone` 后即可看到。内部设计文档仍在本地 `docs/`（被 `.gitignore` 排除，不入库）。

约定：完成一项就删掉对应小节，并把行为变更写进 `CHANGELOG.md`。

## ✅ Emby 4.11 适配主线已完成（2026-11-26）

- 4.11.0.19 真机：34 enabled patches、Healthy、`Failed=0 / WithErrors=0`；MovieDb、DTO、章节、ISO、截图、中文搜索恢复。
- 4.10.2.0 同构建：原配置 33 enabled patches、Healthy、`Failed=0 / WithErrors=0`；开启中文搜索为 34，同时开启通知为 35，均 Healthy。
- 单测 715/715 通过；Release 构建 0 警告 0 错误。
- 单标题 ISO、DTO 无图人物过滤、ImageCapture 主图/背景图、章节标记恢复均完成内容级验收。
- 新增修复：补丁错误不再假报 Healthy；多版本合并/拆分逐库查询与重复判定；4.11 UDP 自定义发现响应序列化；STRM 片头检测范围与 MarkerDetector 放行；空元数据语言下的 TVDB 与单集 DTO 空引用防护；4.11 外挂字幕扫描恢复。
- 发布前安全收尾：深删路径边界与目录保护、多版本 ID 冲突保护、截图原子替换、核心 API 健康生命周期、操作反馈与 DryRun 通知。
- 关闭功能 smoke：MergeMultiVersion、ServerSideVersionLabel、CustomLocalDiscovery（4.10/4.11）、深度删除自定义通知通过。
- 4.11 通知保留原生分组与插件自定义通知；剧集摘要注入确认为不可靠，已跳过相关钩子。4.10 完整通知补丁可用。
- 明确限制：4.11 不挂批量 DTO、不 patch `SaveChapters`；ExclusiveExtract 保持受限模式并关闭；插件指纹使用原生检测。
- `UnlockIntroSkip` STRM 修复通过：真实片头样本两集均被原生任务选中；合成片尾样本的普通 MP4、本地 STRM 与本机 Range STRM 均写入 `CreditsStart` 标记。
- 测试 Webhook、临时库、smoke 媒体、STRM 片尾隔离实例、空语言小库与 Range 服务均已清理；4.10 无效 `simple.dll`、历史 DLL 备份与临时回滚副本已删除。仅余两个被 Windows 句柄锁住的空缓存目录，无数据残留。
- 4.11 适配、发布前安全修复与 STRM 片尾修复已递交推送；空引用防护已本地递交，外挂字幕恢复待递交，升版本号、打 tag 和发版均需用户确认。
- 运行时验收：外挂字幕恢复 DLL（SHA-256 `44b98ad3…`）在隔离 4.11 单电影小库完成外挂 `.srt` 扫描与落库，任务记录 `Item Processed`、字幕错误 0；主 4.11 与 4.10 测试实例均为 Healthy，4.10 旧 Harmony 路径保持正常。
- 本地详细交接：`docs/handoff.md`；实测方案：`docs/architecture/emby-4.11-adaptation-plan-verified.md`（`docs/` 不入库）。

## ✅ 4.0.1.2 片头片尾清除与电影补缺已发布（2026-11-20）

- 功能提交 `b02092f` → `Test` run `44690493786`（Windows + Linux 双 job）全绿；
  发版提交 `0a715c0` → `Test` run `44691032908` 全绿 → tag `v4.0.1.2` →
  `Build Plugin` run `44691071173` 成功 → Release
  [`StrmAssistantPlus v4.0.1.2`](https://github.com/wabisabi926/StrmAssistantPlus/releases/tag/v4.0.1.2)
  已创建，资产 `StrmAssistantPlus_v4.0.1.2.dll`（8,215,040 B，sha256 `5b230600…`，FileVersion 4.0.1.2）。
- 本机 Windows 实测：播放探测关闭时清除按钮可见；电影/单集菜单清除流程到达服务端；
  任务候选 `8 series, 442 movies` 与按新规则计算一致，启动后立即取消、无写库；567 项单测通过。
- **不要移动或重打该 tag**。

## ✅ 4.0.1.1 片头片尾刮削 404 分类已发布（2026-11-17）

- 提交 `82bff1f` → `Test` run `44035367020`（Windows + Linux 双 job）全绿 → tag `v4.0.1.1` →
  `Build Plugin` run `44035376015` 成功 → Release
  [`StrmAssistantPlus v4.0.1.1`](https://github.com/wabisabi926/StrmAssistantPlus/releases/tag/v4.0.1.1)
  已创建，资产 `StrmAssistantPlus_v4.0.1.1.dll`（8,213,504 B，sha256 `c6dd8831…`）。
- 验证：Windows 本机真实 404 三类 code 汇总（`media_not_found=80, no_submissions=72`）；
  NAS 上新传输可用（IntroDb 225 请求成功、写入 116 条标记），TheIntroDB 从 NAS 不可达
  （30 秒超时 → 5 次后熔断），属出网环境限制，非本次改动回归。
- **不要移动或重打该 tag**。

## ✅ 4.0.1.0 ISO 码率 / 语言 / 配置收尾已完成（2026-11-17，Win + NAS 双平台）

同一张真实 4K 原盘的远程 strm（`死侍与金刚狼 (2024) UHD原盘`，7675.4s、2V+6A+10S、
容器 60.9 Mbps）在 Windows 与 NAS 的 Emby 4.10.2.0 上各自 `DeleteMediaInfo` → `ExtractMediaInfo`：

| 平台 | 条目 | 修复前（Emby 等分） | 最终落库 |
|---|---|---|---|
| Win | 28049 | 各 30,428,576 bps | **47,283,051 / 3,584,448 bps** |
| NAS | 287304 | 各 30,428,576 bps | **47,283,051 / 3,584,448 bps** |

用户 BDInfo 真值：4K 基础层 45.20 Mbps、1080p 增强层 3.98 Mbps、视频合计 49.18 Mbps；
当前结果与真值同量级。两端日志一致：`PID count source: uniform-stats`、
`per-stream bitrate from TS byte accounting: 47.28 / 3.58 Mbps`、
`TS video PID bytes: 0x1011=6478656,0x1015=491136 (videoShare=0.8359)`、
`filled 6 audio / 10 subtitle field(s)`、`wrote 1 audio profile label(s)`，无 `probe stream mismatch`。

音频侧同轮补齐：TrueHD 与其 AC-3 核心按顺序对齐并继承语言，6 条音轨 / 10 条字幕全部拿到语言；
按轨道标题与原始文件名写出「配置」文案，TrueHD 为 `Dolby TrueHD + Dolby Atmos`。
注意分母是**全部 PID**（用户明确要求），视频拿 `视频 PID 字节 / 全部 PID 字节` 的份额。

**发版状态**：**已发布**。提交 `80013d6` → `Test` run `44023582529`（Windows + Linux 双 job）
全绿 → tag `v4.0.1.0` → `Build Plugin` run `44024073093` 成功 → Release
[`StrmAssistantPlus v4.0.1.0`](https://github.com/wabisabi926/StrmAssistantPlus/releases/tag/v4.0.1.0)
已创建，资产 `StrmAssistantPlus_v4.0.1.0.dll`（8,210,432 B）。
CHANGELOG / README 已按最终行为更新；**不要移动或重打该 tag**。详见本地 `docs/handoff.md`。

## 待办

### 1. TheIntroDB `mapping` 集序转换（暂缓）

**结论（2026-11-17）**：暂不实现。本机 371/371 部剧的 `DisplayOrder` 均为 `Aired`；多数单集已有 TVDB 单集 id（有 id 时服务端直接精确 pin，不需要 mapping）；且 mapping 只作用于 TheIntroDB 源的 `tvdb_id` 路径，而 NAS 上 TheIntroDB 不可达。当前库无样本、无收益。

**恢复条件**：库里出现 DVD/绝对序的剧，且该集没有 TVDB 单集 id、TheIntroDB 可达时再补。

**背景**：TheIntroDB v4 支持 `mapping=default|official|dvd|alternate|absolute|regional`，用于把调用方的季集号换算成 TVDB default 序。当前只按 Emby 里存的季集号查询，DVD/绝对序的剧集可能取到别的集的数据。

**实现要点**：
- 仅在走 `tvdb_id` 定位时传 `mapping`；`tmdb_id` / `imdb_id` 路径不传
- `Series.DisplayOrder` 映射：`Aired → official`、`Dvd → dvd`、`Absolute → absolute`
- 服务端对不支持的序会返回 400，需回退为不带 `mapping` 重试一次
- 单集已有 `tvdb_episode_id` 时本身已精确 pin，可不传 `mapping`

**验收标准**：找一部 DVD 或绝对序的剧（可临时改 `DisplayOrder` 并刷新元数据），对比加/不加 `mapping` 时同一集取到的片头时间是否对应同一集；日志确认无 400。

### 2. 修复 `test-linux` CI 构建（阻塞 4.0.1.0 发版）

**背景**：2026-11-17 给 `test.yml` 加了 ubuntu-latest job（与 Windows 跑同一套 509 项单测），
构建插件步骤失败（约 8 秒即退出，exit 1）。

**✅ 根因已确认（2026-11-17 取证，非推测）**：不是 ILRepack。
是 `Resource.Embedder` 2.2.0 的 MSBuild Task（`SatelliteAssemblyEmbedderTask`，挂在
`CoreGenerateSatelliteAssemblies` 之后）在 Linux 上解析自身 `ResourceEmbedder.Core.dll` 路径出错：

```
MSBUILD : error : Failed injecting code. Could not find file '/home/runner/work/StrmAssistant/StrmAssistant/StrmAssistant/home\runner\.nuget\packages\resource.embedder\2.2.0\tasks\netstandard2.0\ResourceEmbedder.Core.dll'.
MSBUILD : error : Failed to inject required code into assembly: .../obj/Release/net6.0/StrmAssistant.dll
Build FAILED. (2 Error(s))
```

它用 `$(MSBuildThisFileDirectory)..\tasks\$(TaskFolder)\...` 拼路径，Unix 下反斜杠不作分隔符、
开头的 `/` 又丢了，于是把包根当相对路径拼到项目目录后 → 找不到 DLL → 编译阶段即失败。
日志里没有 `StrmAssistant: ILRepack merge ->`，说明 ILRepack 根本没跑到；
之前 `d7e6894`/`54627ec` 修的 ILRepack 路径不是本次失败点。

**为什么注解只有 exit code**：工作流的 grep 是 `error [A-Z]+[0-9]+|error MSB|error NETSDK`，
匹配不上 `MSBUILD : error :` 这种行 → 没有生成注解。取证需带凭据调 jobs logs API
（本机 `git credential fill` 有缓存 token；匿名 403），详见本地 `docs/handoff.md` 专节。

**已修复（2026-11-17）**：`StrmAssistant.csproj` 把 `Resource.Embedder` 的 PackageReference 加上
`Condition="'$(OS)'=='Windows_NT'"` —— Linux 构建不引入该包、跳过卫星注入，Windows 发布构建
（`build.yml` 跑 windows-latest）行为不变。`test.yml` 的 grep 正则补上 `MSBUILD : error` /
`Failed injecting` / `Build FAILED`，让今后这类失败直接进注解。
本地用 `-p:OS=Unix` 模拟过 Linux 构建：跳过 `Finished embedding cultures`、ILRepack 正常、0/0。

构建修复后 CI 又暴露一个被 Windows-only CI 掩盖的用例缺陷：`VersionIdentityTests` 硬编码
`@"D:\TV\Show\..."`，Linux 上 `\` 不是分隔符 → `Path.GetDirectoryName` 返回空串 →
`RejectsDifferentFolder` 两个目录判等（期望 False 得 True）。已改 `Path.Combine`（`072389a`），
生产代码无问题（路径来自 Emby）。

**✅ CI 复验通过（run `43998522801`）**：`test` 与 `test-linux` 两 job 均 success；
Linux job `Build succeeded` + ILRepack 跑到 + 509/509 passed。本待办完成。

### 3. NAS 真盘验证（发版门槛）— ✅ 已完成（2026-11-17，合成 300 段盘，本地 ISO + 远程 strm 双路径）

**环境**：内网 NAS 的 Emby 4.10.2.0（Linux）。本机无 Docker、WSL 被禁，故用本仓自建的
300 段无缝分支合成 ISO（119,197,696 B，600s，2 路视频 4K+1080p + TrueHD）代替真实盘——
它正是当初复现「>256 段被拒」的样本，验的是同一条 Linux 采样链路。
通过 SMB 放文件 + Emby API 建库/触发，全程可复现（手册见本地 `docs/dev/nas-verify-manual.md`）。

**断言全部通过**（日志原文 + API 落库双向核对）：

| 断言 | 结果 |
|---|---|
| 原生挂载不可用 → 进程内 UDF 兜底 | ✅ `native mount not supported ... falling back to in-process UDF` |
| playlist 300 段 | ✅ `selected playlist 00000.MPLS: 300 play items` |
| 均匀采样 | ✅ `probe summary: ... 256/300 clips` |
| 探测出 2 路视频 | ✅ `probe summary: ... probe=2V/2A/0S` |
| 码率按正片字节修正 | ✅ `corrected bitrate from title bytes: 1.6 Mbps (117964800 bytes / 600.0s, 2 video stream(s))` |
| 无少流告警 | ✅ `probe stream mismatch` 命中 0 |
| 落库媒体信息 | ✅ `Container=mpegts`、`RunTimeTicks=6000000000`(600s)、2 路 hevc 视频(3840x2160 + 1920x1080) + TrueHD/AC-3 |
| 临时文件落点 | ✅ `/vol1/@appdata/embyserver/cache/temp/strmassistant_iso/`（Emby cache，非 /tmp） |

**注意**：`TotalBitrate` 字段经 API 读为 None，但日志的 `corrected bitrate` 与 `-mediainfo.json`
落盘证明修正逻辑执行了；API 层码率字段的呈现差异不阻塞本验证（与本机 Windows 行为一致）。
**远程 strm（ISO over HTTP Range）同日补验通过**：本机起 `0.0.0.0` Range 服务器，strm 指向
`http://<LAN IP>:18101/multi300.iso`；注意 strm 入库必须触发 `Scan media library` 计划任务
（`Items/Refresh` 发现不了新 strm）。日志 `remote title sample saved` / `remote iso probing via
title sample` / `probe summary: 256/300 clips, probe=2V/2A/0S` / `corrected bitrate 1.6 Mbps`，
无 mismatch，落库 mpegts/600s/2 路 hevc。测试用临时库/文件已清理，**ISO 测试库与文件保留在 NAS**
（`strmassistant-iso-test`），便于复测；验证用 API Key 不入库。

### 4. ISO 采样精度与弱网（P1）

- ~~单流码率改按样本内各 PID 字节占比分配~~ ✅ 已完成（`TsByteAccounting`）：ffprobe 对 mpegts 的
  视频流一律给 `bit_rate=N/A`，Emby 只能把容器码率**等分**给各路视频，带画中画的原盘会把 PiP
  报成和主画面同码率（实测样本两路视频字节 214080 : 78912 ≈ 73% : 27%，界面显示 50% : 50%）。
  现按探测输入的 TS PID 字节占比还原真实比例，分母含全部基本流（用户明确要求，不要改成视频 PID 之和）；
  PMT 声明视频 PID 数与探测视频流数不一致时跳过，宁可不改。日志
  `per-stream bitrate from TS byte accounting: …`。
- ✅ 单段长 m2ts 的头尾采样会低估视频占比（`29.60 / 5.05` vs BDInfo `45.20 / 3.98`）：
  探测样本保持不动，另写沿全片 32 窗口的 `.stats.ts` 只供 PID 字节统计（约 8MB）。
  两端最终 `47.28 / 3.58`，见文首完成记录。
- 远程采样预算自适应（弱网降到 32MB/128 段），需真机弱网验证。

### 5. STRM 原生片头/片尾内容验收（部分完成）

- 本机 4.11 使用《武林外传》S01E01/E02 两个真实 STRM：片头任务完成，两集均生成音频指纹并识别出同一段约 97.3 秒片头，置信度 0.9。
- 片尾任务已补齐 `CreditsScheduledTask` 查询范围与 `MarkerDetector.IsCreditsDetectionEnabled` 的 STRM 放行；启用时仍遵守媒体库的片尾检测开关。
- 隔离 `CreditLab` 真机验收：普通 MP4、本地 STRM、两条本机 HTTP Range STRM 均完成原生分析并写入 `CreditsStart=52:00`；最终 DLL SHA-256 `0ed50e81…`，启动 `Healthy`、`UnlockIntroSkip=Applied`。
- 合成样本为 60 分钟视频，最后 8 分钟含黑底高亮与字幕变化，用于满足原生黑帧、上下文和稳定性阈值；该证据验证 STRM 链路，不替代真实影片内容验收。
- 真实 NAS 源片尾内容仍待家庭局域网恢复后复验；本轮不访问 NAS。

### 6. 真实多 m2ts 本地 ISO

使用真实多 m2ts 本地 ISO 验证正片选择、流数量、码率、章节和语言；现有证据为单标题与 300 段合成盘，不能替代真实多 m2ts 样本。

### 7. 4.11 外挂字幕稳定入口 — ✅ 已完成（2026-11-27）

- `SubtitleResolver` 旧方法名已移除，改为按 `BaseTrackResolver(BaseItem,int,IDirectoryService,LibraryOptions,NamingOptions,bool) -> List<MediaStream>` 稳定签名解析。
- 外挂字幕探测更新器按 `IMediaProbeManager` 构造参数和 `(BaseItem,MediaStream,MetadataRefreshOptions,LibraryOptions,CancellationToken) -> Task<bool>` 唯一签名解析；4.11 使用 Reflection，不再对混淆方法名做 Harmony 逆向补丁。
- 4.10 保持原类型名与 Harmony 路径；两套实例启动均 Healthy。
- 隔离 4.11 单电影小库：初始无外挂字幕，新增 `.srt` 后运行“扫描外挂字幕与音轨”，DTO 落库 `IsExternal=True`、语言 `zh`，日志 `ExternalSubtitle - Item Processed`，字幕错误 0。
- 全量 715/715 通过，Release 构建 0 警告 0 错误；最终 DLL SHA-256 `44b98ad37187f25bf287970cc39696e6950e0ca9a90f20f0fbb9852894bdf496`。

### 8. 4.9 / ARM64 回归（仅用户需要时）

具备对应设备后再跑同一套启动健康、中文搜索、ISO、截图、章节、多版本和通知冒烟；未验收前不承诺完整功能一致。

## 观察项（4.0.0.9 上线后）

任务日志收尾三行用于判断是否需要下一个版本：

1. `Mixed-source merges`：长期为 0 说明“完整源优先”足够；若频繁出现，说明两源都经常不完整，需要评估补全策略
2. `Request failures: status 400`：出现即说明 ID 组合还有边界情况（例如某类媒体不接受当前参数组合）
3. `disabled for the rest of this run`：说明某源在该网络下连续失败被熔断，需要评估超时与降级阈值

## 已知限制

- `no_intro` / `no_outro` 标记未参与决策：实测存在反例（TheIntroDB 有片头而 IntroDb 标记无片头），信任它会漏标
- 混源拼接（两源各缺一半时）无法避免，已记录 `mixed` 计数与同字段差值，超过 30s 打 Warn
