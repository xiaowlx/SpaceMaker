# SpaceMaker 空间魔术师

一个 Windows 桌面小工具：在指定磁盘上"占用"自定义大小的空间，可一键释放。支持两种模式，带现代界面（亮/暗主题、侧边栏、自动更新入口）。

![SpaceMaker icon](Assets/icon.ico)

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

## 运行方式

### 方案 A：依赖框架运行（推荐，体积小）

需要目标机器已安装 **.NET 9 Desktop Runtime**（或 SDK）。首次运行前若不确定是否安装，请双击 `SpaceMaker.Launcher.cmd`：

- 已安装 .NET 9 运行时 → 直接启动 `SpaceMaker.exe`
- 未安装 → 自动打开官方下载页：`https://dotnet.microsoft.com/download/dotnet/9.0`

若已确认安装，可直接双击 `SpaceMaker.exe`。

### 方案 B：独立运行（体积大）

自包含单文件发布，不依赖系统运行时，产物约 130 MB，命令见下方「构建与发布」。

## 构建与发布

需要 .NET 9 SDK（或更高）。

```bash
# 默认发布：依赖框架，产物约 30 MB
# 输出目录：publish_build/
dotnet publish -c Release -o publish_build

# 独立运行（自包含单文件），产物约 130 MB
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish_build

# 调试运行
dotnet run -c Debug
```

发布产物（`publish_build/`）说明：

| 文件/目录 | 说明 |
| --- | --- |
| `SpaceMaker.exe` | 程序入口（apphost，体积约 280 KB） |
| `SpaceMaker.Launcher.cmd` | 运行前检测 .NET 9 运行时并引导下载 |
| `*.dll` / `*.json` | Avalonia、Skia 等依赖及运行配置 |

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
│   ├── bin/              # 构建中间输出
│   ├── obj/              # 编译临时文件
│   ├── publish_build/    # 最终发布的 exe/dll
│   └── *.exe             # 生成的可执行文件
└── 主页/介绍文档（随仓库一起提交）
    └── README.md         # 本说明
```

`.gitignore` 已把 `bin/`、`obj/`、`publish_build/`、`*.exe`、`crash.log` 排除在外，因此直接 `git push` 只会上传源码与 README。

## 许可证

MIT
