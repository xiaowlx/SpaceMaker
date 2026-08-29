# SpaceMaker 空间魔术师

一个 Windows 桌面小工具：在指定磁盘上"占用"自定义大小的空间，可一键释放。支持两种模式，带现代界面（亮/暗主题、侧边栏、自动更新入口）。

![SpaceMaker icon](Assets/icon.ico)

## 下载：该选哪个版本？

每次发布都提供两个包，**功能完全一样**，区别只有一件事：**要不要先装 .NET 9 运行时**。

| | 依赖框架版（framework） | 独立版（standalone） |
| --- | --- | --- |
| 文件 | `SpaceMaker-v2.0.1-win-x64-framework.zip` | `SpaceMaker-v2.0.1-win-x64-standalone.zip` |
| 压缩包体积 | 约 12 MB | 约 45 MB |
| 解压后 | 约 30 MB / 35 个文件 | 约 100 MB / 200+ 个文件 |
| 要先装 .NET 9 运行时 | **要** | **不要** |
| 解压后怎么启动 | 有运行时就双击 `SpaceMaker.exe`；不确定就双击 `SpaceMaker.Launcher.cmd` | 直接双击 `SpaceMaker.exe` |

### 一句话选择

- **不确定电脑有没有 .NET 9，或者不想折腾** → 下 **独立版**，解压即用。
- **知道自己装了 .NET 9（或愿意花一分钟装一次）** → 下 **依赖框架版**，体积小得多，以后升级也只换几个文件。
- **要拷到别人电脑、离线机器、PE/测试环境** → 下 **独立版**，那边不一定有运行时，也不一定有网。
- **本机长期自用、追求精简** → 下 **依赖框架版**。

### 怎么知道有没有装 .NET 9？

在终端（PowerShell / 命令提示符）里执行：

```
dotnet --list-runtimes
```

看输出里有没有以 `Microsoft.NETCore.App 9.` 开头的行。有就装了，没有就没装。
（SpaceMaker 基于 Avalonia，不需要 WPF/WinForms 的 WindowsDesktop 运行时，普通 .NET Runtime 就够。）

更简单的方法：解压依赖框架版后双击 `SpaceMaker.Launcher.cmd` —— 它会自动检测，装了就直接启动程序，没装就打开官方下载页。

> .NET 9 是标准支持（STS）版本，2026 年 11 月 10 日终止支持。运行时本身不随 Windows 11 预装，需要单独安装一次。

### 下载

👉 <https://github.com/xiaowlx/SpaceMaker/releases/latest>

## 功能特性

- 两种占用模式：真占用 / 稀疏文件
- 首次打开默认即为**真占用**，模式切换只能用鼠标点选，避免滚轮误触
- 亮暗主题实时切换，窗口按屏幕自动缩放
- 设置自动保存（无需点"保存"按钮）
- 可选"双击即管理员运行"
- 启动时自动找回上次残留的占用文件，避免空间被悄悄吃满
- 真占用模式可一键把 `SeManageVolumePrivilege` 写入/移除当前账户

## 两种模式对比

| 模式 | 是否真占空间 | 是否需要特权 | 说明 |
| --- | --- | --- | --- |
| 真占用 (Real) | 是 | 需要 `SeManageVolumePrivilege` | 用 `SetFileValidData` 分配簇但不写数据，瞬间完成 |
| 稀疏文件 (Sparse) | 否 | 不需要 | 文件显示很大，实际不占空间 |

## 关于真占用与 `SeManageVolumePrivilege`

真占用依赖 Windows 的 **`SeManageVolumePrivilege`（执行卷维护任务）** 特权。仅有管理员身份还不够——该特权需明确授予账户：

- 在 SpaceMaker 选「真占用」模式时，提示卡会出现两个按钮：
  - **自动授予卷维护特权**：通过 LSA 策略把该特权写入当前账户（会弹 UAC）。授予后需**注销并重新登录**才生效。
  - **恢复默认（撤销特权）**：从当前账户移除该特权，回到系统默认。
- 也可以手动配置：`Win + R` → `secpol.msc` → 本地策略 → 用户权限分配 → 双击「执行卷维护任务」→ 添加账户。
- 系统内置的 `Administrator` 账户默认就拥有该特权。

## 构建与发布

需要 .NET 9 SDK（9.0.x）。默认构建出的是**依赖框架版**，加 `-p:SelfContained=true` 出**独立版**：

```bash
# 依赖框架版：产物约 30 MB
dotnet publish -c Release -o publish-fd

# 独立版：自带 .NET 9 运行时，产物约 100 MB
dotnet publish -c Release -p:SelfContained=true -o publish-sc

# 调试运行
dotnet run -c Debug
```

Release 构建不带调试符号：本程序集不生成 pdb，NuGet 包里的 native 符号也不复制。否则光一个 `libSkiaSharp.pdb` 就是 84 MB，发布目录会膨胀到 130 MB。

依赖框架版的产物说明：

| 文件 | 说明 |
| --- | --- |
| `SpaceMaker.exe` | 程序入口（apphost，约 280 KB） |
| `SpaceMaker.Launcher.cmd` | 启动前检测 .NET 9 运行时并引导下载（仅依赖框架版附带） |
| `*.dll` / `*.json` | Avalonia、Skia 等依赖及运行配置 |

打包分发用的脚本 `pack_release.py` 放在仓库外，用法：

```bash
python pack_release.py 2.0.1 <依赖框架版目录> <独立版目录> <zip 输出目录>
```

它会把两个目录分别打成 `SpaceMaker-v2.0.1-win-x64-framework.zip` 与 `SpaceMaker-v2.0.1-win-x64-standalone.zip`。

### 构建时若提示 obj 被占用

出现 `CS2012 无法打开 obj\...\SpaceMaker.dll` 或 `MSB3491 ... Access is denied`，通常是有其它进程（杀软、索引器、上一次残留的编译进程）占住了 obj 里的文件。先关掉常驻编译服务：

```bash
dotnet build-server shutdown
```

仍不行就把中间目录挪开，两种模式各用一套：

```bash
dotnet publish -c Release -p:BaseIntermediateOutputPath=".build\obj-fd\" -p:BaseOutputPath=".build\bin-fd\" -o publish-fd
dotnet publish -c Release -p:SelfContained=true -p:BaseIntermediateOutputPath=".build\obj-sc\" -p:BaseOutputPath=".build\bin-sc\" -o publish-sc
```

## 数据与隐私

- 占用记录与设置保存在 `%LOCALAPPDATA%\SpaceMaker\`（不会在 exe 目录外写入无关文件）。
- 全局未处理异常会写入 `%LOCALAPPDATA%\SpaceMaker\crash.log`。
- 真占用创建的是空壳文件（位于各盘根目录隐藏的 `.spacemaker\` 文件夹），释放即删除，不含任何用户数据。

## 自动更新

自动更新接口已预留（`Update.cs` 中的 `IUpdateSource`）。当前为占位实现（未配置更新源），后续可接入 GitHub Releases：新增一个 `IUpdateSource` 实现并在 `MainWindow` 中替换 `_updater` 即可。

## 项目文件分类

```
SpaceMaker/
├── 开源代码（直接托管到 GitHub）
│   ├── *.cs              # 程序源码（MainWindow、Store、DiskEngine 等）
│   ├── *.axaml           # Avalonia XAML / 样式
│   ├── SpaceMaker.csproj # 工程配置
│   ├── app.manifest      # Windows 管理员权限清单
│   ├── Assets/           # 图标、字体等资源
│   └── SpaceMaker.Launcher.cmd  # 启动脚本
├── 发布产物（不应提交到 GitHub）
│   ├── bin/ obj/         # 构建中间输出
│   ├── .build/           # 自定义中间目录（obj 被占用时才用）
│   ├── publish-fd/       # 依赖框架版产物
│   ├── publish-sc/       # 独立版产物
│   └── *.exe             # 生成的可执行文件
└── 主页/介绍文档（随仓库一起提交）
    └── README.md         # 本说明
```

`.gitignore` 已把 `bin/`、`obj/`、`publish_build/`、`*.exe`、`crash.log` 排除在外，因此直接 `git push` 只会上传源码与 README。

## 许可证

MIT
