# Vast-Print

[![Build](https://github.com/vast-huhh/vast-printf/actions/workflows/build.yml/badge.svg)](https://github.com/vast-huhh/vast-printf/actions/workflows/build.yml)

`Vast-Print` 是一个基于 `.NET 8 + WPF` 的 Windows 桌面批量打印工具。它面向“把一批文件按顺序送去打印”这个场景，支持导入文件、扫描文件夹、按文件类型过滤、暂停/继续/取消任务、记录打印历史和日志。

## 这是什么工程

- 工程类型：Windows 桌面应用
- UI 技术：`WPF`
- 运行平台：`net8.0-windows`
- 主要用途：多文件批量打印
- 解决方案文件：`VastPrint.sln`
- 主项目：`VastPrint.App/`

核心打印链路大致如下：

- PDF 使用内置渲染方案打印，不依赖外部 PDF 阅读器
- 图片、文本、RTF、XPS 直接走对应渲染器
- Office 文档先转换为 PDF，再复用 PDF 打印链路
- 设置、打印历史和日志写入 `%LOCALAPPDATA%\VastPrint`

## 主要功能

- 批量导入单个文件、多个文件或整个文件夹
- 支持拖拽文件 / 文件夹到主窗口
- 支持按扩展名过滤导入范围
- 支持打印整个队列或仅打印选中项
- 支持暂停、继续、取消任务
- 支持失败项重试
- 支持调整打印队列顺序
- 支持打印机属性和页面设置
- 支持彩色 / 黑白、单面 / 双面、纸张方向、边距等配置
- 支持打印历史记录和本地日志

## 支持的文件格式

- PDF：`.pdf`
- 图片：`.png` `.jpg` `.jpeg` `.bmp` `.gif` `.tif` `.tiff`
- 文本：`.txt` `.log` `.csv` `.json` `.xml` `.md` `.ini`
- 富文本：`.rtf`
- 固定版式：`.xps`
- Office 文档：`.doc` `.docx` `.xls` `.xlsx` `.ppt` `.pptx`

## Office 文档打印说明

Office 文档在源码里是支持的，但实现方式不是“直接打印原文件”，而是：

1. 先把 Office 文档转换为临时 PDF
2. 再把 PDF 送入统一的 PDF 打印链路

转换时的优先级是：

1. 优先使用 LibreOffice
2. 如果找不到 LibreOffice，则尝试调用本机安装的 Microsoft Office COM 组件
3. 两者都没有时，Office 文档无法打印

如果你是通过本项目生成的安装包安装软件，通常可以直接使用 Office 文档打印功能，因为安装脚本会把构建机上的 LibreOffice 目录一并打包到应用目录。

更具体地说：

- 安装后的程序会优先查找 `应用目录\LibreOffice\program\soffice.exe`
- 当前安装脚本会把 `LibreOfficeSourceDir` 指向的 LibreOffice 文件复制到安装目录
- 这意味着“安装包版”和“直接运行源码版”的 Office 文档支持条件不完全一样

所以要分两种情况看：

- 如果你安装的是你打出来的正式安装包，并且打包时 `installer/VastPrint.iss` 中的 `LibreOfficeSourceDir` 是有效的，那么安装后一般就可以直接打印 `.doc/.docx/.xls/.xlsx/.ppt/.pptx`
- 如果别人只是从 GitHub 下载源码自己运行，那么源码仓库本身并不包含 LibreOffice，此时仍需要满足下面任意一项：

- 本机已安装 LibreOffice
- 本机已安装 Microsoft Word / Excel / PowerPoint

## 运行环境

普通使用者：

- `Windows 10` 或 `Windows 11`

开发者：

- `Windows 10` 或 `Windows 11`
- `.NET 8 SDK`
- Visual Studio 2022，或已安装 Windows Desktop 相关构建工具

如果你要重新生成安装包，还需要：

- `Inno Setup 6`

## 如何使用这份源码

### 1. 获取源码

```powershell
git clone <your-repo-url>
cd vast
```

### 2. 还原依赖

在仓库根目录执行：

```powershell
dotnet restore VastPrint.sln
```

仓库里保留了 `NuGet.Packaging.config`，用于声明包源。项目默认依赖 `Docnet.Core`。

如果你的网络环境无法访问 `nuget.org`，可以自己创建 `local-packages/` 目录，并放入对应的 `.nupkg` 文件后再还原。

### 3. 编译项目

```powershell
dotnet build VastPrint.sln -c Release
```

### 4. 直接运行源码

开发时可以直接运行主项目：

```powershell
dotnet run --project .\VastPrint.App\VastPrint.App.csproj -c Debug
```

如果你想验证发布配置，也可以改成 `Release`。

### 5. 软件内的基本使用流程

1. 启动程序后先选择打印机
2. 点击“打印机属性”或“页面设置”确认纸张、方向、单双面、彩色模式等参数
3. 通过“添加文件”或“添加文件夹”导入待打印内容
4. 如需限制导入类型，先打开“设置”勾选允许的扩展名
5. 点击“开始打印”执行整个队列，或点击“打印选中项”只处理部分文件
6. 打印过程中可暂停、继续或取消
7. 在“打印历史”区域查看结果，在日志文件里排查失败原因

## 本地数据位置

程序会把设置、历史和日志写入：

```text
%LOCALAPPDATA%\VastPrint
```

主要文件包括：

- `appsettings.json`：打印设置、导入规则、页面设置
- `print-history.json`：打印历史
- `logs\vastprint-yyyyMMdd.log`：日志文件

## 构建发布版

### 1. 发布自包含目录

```powershell
dotnet publish .\VastPrint.App\VastPrint.App.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -o .\artifacts\publish\win-x64
```

发布结果会输出到：

```text
artifacts\publish\win-x64
```

### 2. 生成安装包

安装脚本位于：

```text
installer\VastPrint.iss
```

生成安装包前请注意两件事：

- `installer\prereqs\VC_redist.x64.exe` 需要你自己准备
- `installer\VastPrint.iss` 里的 `LibreOfficeSourceDir` 需要指向一份真实存在的 LibreOffice 安装目录

当前脚本的做法是“把该目录下的 LibreOffice 文件直接复制进安装包”，而不是在安装时再额外下载或执行 LibreOffice 安装器。

然后使用 Inno Setup 的 `ISCC.exe` 编译脚本即可。

## 项目结构

```text
VastPrint.App/
├─ Assets/          图标等资源
├─ Infrastructure/  基础命令与通知模型
├─ Interop/         Win32 / 打印相关原生调用
├─ Models/          数据模型与设置模型
├─ Renderers/       各类文件的打印渲染器
├─ Services/        文件选择、发现、打印执行、设置、日志等服务
├─ ViewModels/      主界面与弹窗逻辑
└─ Views/           WPF 窗口界面

installer/
└─ VastPrint.iss    Inno Setup 安装脚本
```

## 共享到 GitHub 时建议保留什么

建议上传：

- `VastPrint.sln`
- `VastPrint.App/`
- `installer/VastPrint.iss`
- `.gitignore`
- `README.md`
- `NuGet.Packaging.config`
- 你希望保留的设计文档，例如 `windows-multi-file-print-plan.md`
- 你确认仍然需要的源图标文件

## 共享到 GitHub 时不建议上传什么

这些内容通常不应该进源码仓库：

- `artifacts/`
- `.build-out*/`
- `.dotnet-cli-home/`
- `.nuget-packages/`
- `local-packages/`
- `installer/prereqs/`
- `bin/` 和 `obj/`
- `.vs/`、`.vscode/`、`.idea/` 等 IDE 临时文件

仓库里的 `.gitignore` 已经按这个思路做了处理。

## 备注

- 这是一个 Windows 专用项目，不能在 macOS / Linux 上直接构建 WPF 可执行程序
- 如果你准备公开分享到 GitHub，建议额外补一个 `LICENSE`
- 如果你准备发布给非开发者使用，建议把编译产物放到 GitHub Releases，而不是直接提交到仓库
