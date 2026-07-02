<img src="media/ShuShuLogo.png" height=100 align=right>

# 鼠鼠小账本 / ShuShuscanner

[English README](README.md)

ShuShuscanner 是 [RatScanner/RatScanner][upstream-ratscanner] 的非官方个人 fork。它是一个面向 [Escape from Tarkov][escape-from-tarkov] 的外部物品扫描工具。

这个分支主要维护中文本地化、界面调整、自定义品牌，以及适合个人使用的发布版本。

本项目不隶属于原 RatScanner 作者，也不由原作者维护。原项目版权声明和许可证信息会按项目许可证保留。

<br/>

## 免责声明

ShuShuscanner 是基于截图识别的外部工具。它不会读取或修改游戏内存。

Battlestate Games 与本项目无关。请自行判断并承担使用风险。

<br/>

## 这个分支改了什么

- 应用名称改为 ShuShuscanner
- 中文界面本地化，中文名“鼠鼠小账本”
- 更新了 logo 和应用图标
- 调整了设置界面布局
- 添加扫描过程状态信息显示，并可在常规设置中开关
- 状态信息默认关闭
- 名称扫描和图标扫描支持分别配置快捷键
- 改进 PvE 数据刷新行为

当前版本：`1.0.2`

<br/>

## 功能说明

ShuShuscanner 可以在游戏中扫描物品，并显示物品信息，例如平均价格、每格价值、商人价格等。

物品数据来自 [tarkov.dev][tarkov-dev]，该服务的数据来源于游戏数据。

<br/>

## 工作方式

这个工具完全在游戏外部运行，不读取游戏内存。

扫描时，程序会截取屏幕图像，然后通过图像处理识别你点击的物品。识别到物品后，程序会在数据库中查找对应信息，并在窗口和悬浮提示中显示结果。

<br/>

## 使用方式

为了让悬浮提示正常显示，游戏可能需要使用 `无边框` 或 `窗口化` 模式。

目前有两种扫描方式。

### 名称扫描

名称扫描用于识别物品检查窗口中的名称。

- 在物品检查窗口中，点击放大镜图标即可扫描

限制：

- 耐久和使用次数默认按 100% 处理
- 武器和其他可改装物品只会显示基础物品信息

<img src="media/NameScan.gif" width=400px>

### 图标扫描

图标扫描用于识别物品图标。

- 按住修饰键并左键点击物品
- 默认修饰键是 `Shift`
- 快捷键可以在设置中修改

限制：

- 当前无法可靠扫描武器
- 耐久和使用次数默认按 100% 处理
- 多个物品共用相似图标时，可能出现不确定匹配
- 仓库左上角的物品可能受屏幕上方亮光影响，导致误匹配

<img src="media/IconScan.gif" width=400px>

<br/>

## 精简界面

可以通过标题栏按钮切换到精简界面。  
在精简界面中双击窗口任意位置可以返回标准界面。

背景透明度和显示的信息项可以在设置中调整。

<img src="media/MinimalUI-HowTo.gif" width=280px>

<br/>

## 开发环境

1. 克隆仓库
2. 从最新 release 中复制 `Data` 文件夹到 `ShuShuscanner\Data\`

### 编译

- 用 Visual Studio 打开解决方案，然后执行 Build -> Build Solution
- 或在仓库根目录运行：

```powershell
dotnet build RatScanner.sln
```

### 发布

- 运行仓库根目录中的 `publish.bat`
- 输出会生成在同级的 `publish` 文件夹中

<br/>

## 贡献

贡献前请阅读 `CONTRIBUTING.md`。

<br/>

## 许可证和归属

本项目基于 [RatScanner/RatScanner][upstream-ratscanner]。

原始版权和许可证声明会保留。本 fork 仍受原仓库许可证约束。

[escape-from-tarkov]: https://www.escapefromtarkov.com/
[tarkov-dev]: https://tarkov.dev/
[upstream-ratscanner]: https://github.com/RatScanner/RatScanner
