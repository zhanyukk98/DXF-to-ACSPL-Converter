# DXF to ACSPL Converter v2.0 
## 专业级DXF激光加工路径规划与控制代码生成工具

<div align="center">

[![.NET](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/download)

[![Windows](https://img.shields.io/badge/Platform-Windows-blue.svg)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/License-Proprietary-orange.svg)](LICENSE)

**一个为激光打孔、数控加工等自动化设备提供的专业DXF文件处理和ACSPL运动控制代码生成工具**

</div>

---

## 📋 目录

- [项目概述](#-项目概述)
- [核心功能](#-核心功能)
- [技术架构](#-技术架构)
- [系统要求](#-系统要求)
- [安装部署](#-安装部署)
- [用户指南](#-用户指南)
- [开发文档](#-开发文档)
- [算法详解](#-算法详解)
- [性能指标](#-性能指标)
- [更新日志](#-更新日志)

---

## 🎯 项目概述

DXF to ACSPL Converter v2.0 是一个基于**现代化架构设计**的专业级CAD文件处理工具，专门为**激光打孔、数控加工、PCB钻孔**等工业自动化应用场景而设计。项目采用分层架构，集成了**自研Rust高性能DXF解析引擎**，专门针对圆形、椭圆、多段线拟合圆的解析和归一化处理进行优化，提供智能路径规划和ACSPL运动控制代码生成功能。

### 🎯 应用场景

- **激光打孔设备**: PCB板、薄膜材料的精密打孔
- **数控钻床**: 自动化钻孔加工路径规划
- **激光切割机**: 圆形工件的批量加工
- **自动化产线**: 工业机器人的运动控制编程
- **原型制造**: 快速原型制作中的精密加工

### 🔧 核心价值

- **自研解析引擎**: 基于Rust开发的高性能DXF解析器，专门优化圆形实体处理
- **智能路径优化**: 多种算法(聚类、螺旋、蛇形)实现最短路径规划
- **工业级代码生成**: 生成符合ACSPL标准的高质量运动控制代码
- **现代化架构**: 分层设计，高可维护性和扩展性
- **实时预览**: 高性能SVG预览，支持大型图纸文件

## 🏗️ 项目结构

```
dxf2-acspl/
├── DXFtoACSPL.Core/              # 核心业务逻辑库
│   ├── Interfaces/               # 接口定义
│   │   ├── IDxfParser.cs         # DXF解析器接口
│   │   ├── IPathGenerator.cs     # 路径生成器接口
│   │   ├── ICodeGenerator.cs     # 代码生成器接口
│   │   └── IDataService.cs       # 数据服务接口
│   ├── Models/                   # 数据模型
│   │   ├── CircleEntity.cs       # 圆形实体模型
│   │   ├── PathElement.cs        # 路径元素模型
│   │   ├── ProcessingConfig.cs   # 处理配置模型
│   │   ├── PathGenerationAlgorithm.cs # 算法枚举
│   │   └── PointData.cs          # 点数据模型
│   ├── Parsers/                  # DXF解析器实现
   │   ├── DxfFastAdapter.cs     # Rust解析器适配器
   │   └── DxfParser.cs          # 基础netDxf解析器
│   ├── Services/                 # 业务服务实现
│   │   ├── PathGenerator.cs      # 路径生成器
│   │   ├── ACSPLCodeGenerator.cs # ACSPL代码生成器
│   │   ├── JsonDataService.cs    # JSON数据服务
│   │   ├── PathAnalyzer.cs       # 路径分析器
│   │   ├── SpatialIndex.cs       # 空间索引优化
│   │   ├── SpiralFillPathGenerator.cs # 螺旋填充生成器
│   │   └── SpiralFillACSPLGenerator.cs # 螺旋ACSPL生成器
│   └── Libs/                     # 依赖库文件
       └── dxf_fast_ffi.dll      # Rust解析器FFI库
├── DXFtoACSPL.WinForms/          # Windows窗体应用
│   ├── Forms/                    # 窗体文件
│   │   ├── MainForm.cs           # 主窗体
│   │   ├── MainForm.Designer.cs  # 主窗体设计器
│   │   ├── ConfigForm.cs         # 配置窗体
│   │   └── PathCoordinatesForm.cs # 路径坐标窗体
│   ├── Controls/                 # 自定义控件
│   │   ├── CirclesDataGridView.cs # 圆形数据表格
│   │   ├── DxfPreviewControl.cs  # DXF预览控件
│   │   ├── EnhancedSvgPreviewControl.cs # 增强SVG预览
│   │   ├── PathVisualizationControl.cs # 路径可视化控件
│   │   ├── Direct2DDxfPreviewControl.cs # Direct2D预览控件
│   │   └── SkiaSharpDxfPreviewControl.cs # SkiaSharp预览控件
│   ├── Libs/                     # 本地依赖库
│   ├── Program.cs                # 程序入口点
│   └── app.manifest              # 应用程序清单
├── test_circles.dxf              # 测试DXF文件
├── DXFtoACSPL.sln               # Visual Studio解决方案
├── LICENSE                       # 许可证文件
├── README.md                     # 项目说明文档
└── 重构总结.md                    # 重构总结文档
```

---

## 🚀 核心功能

### 🔍 DXF文件解析引擎

#### 自研Rust解析器
- **专用优化**: 专门针对圆形、椭圆、多段线拟合圆的高性能解析
- **格式兼容性**: 支持AutoCAD R12-2024版本DXF文件
- **实体支持**: 圆形、圆弧、椭圆、多段线圆形拟合、块引用等
- **归一化处理**: 自动将椭圆和多段线转换为标准圆形实体
- **内存优化**: Rust零拷贝技术，大文件(>100MB)处理优化
- **FFI接口**: 通过C FFI与.NET无缝集成

#### 专用实体识别
```rust
// 自研Rust解析器专门支持的实体类型
- CIRCLE: 标准圆形实体 (直接解析)
- ARC: 圆弧实体 (智能补全为圆形)
- ELLIPSE: 椭圆实体 (归一化为等效圆形)
- LWPOLYLINE: 轻量多段线 (圆形拟合算法)
- POLYLINE: 多段线 (包含圆弧段拟合)
- INSERT: 块引用 (递归解析圆形实体)
```

### 2. 🎯 圆形检测与归一化

#### Rust高性能圆形拟合算法
- **专用优化**: 针对圆形、椭圆、多段线的专门拟合算法
- **归一化处理**: 将所有圆形类实体统一转换为标准圆形
- **容差控制**: 可配置检测精度(默认1.5mm)
- **半径过滤**: 自定义最小/最大半径范围
- **中心点去重**: 消除重复圆形(容差0.1mm)
- **零拷贝优化**: Rust内存安全保证下的高性能处理

#### 数据处理管道
```
DXF文件 → Rust解析器 → 实体归一化 → 圆形拟合 → 坐标变换 → 数据清洗 → 输出结果
          ↓
      FFI接口传输
          ↓
      .NET业务逻辑
```

### 3. 🗺️ 智能路径规划算法

#### 6种路径规划算法

| 算法类型 | 适用场景 | 优化目标 | 性能特点 |
|---------|---------|---------|---------|
| **聚类算法** | 分布式打孔 | 最短总距离 | 快速收敛 |
| **螺旋填充** | 密集打孔 | 连续路径 | 高精度 |
| **蛇形路径** | 规则排列 | 行扫描 | 高效率 |
| **最近邻** | 小批量加工 | 贪心优化 | 简单快速 |
| **增强聚类** | 复杂图案 | 自适应网格 | 智能优化 |
| **测试算法** | 自定义路径 | 灵活配置 | 可扩展 |

#### 阿基米德螺旋算法详解
```csharp
// 螺旋路径生成参数
public class SpiralFillConfig
{
    public float SpiralRadiusIncrement { get; set; } = 1.0f;  // 半径增量dr
    public float SpiralAngleStep { get; set; } = 0.1f;       // 角度步长dθ
    public float SpiralStartRadius { get; set; } = 0.0f;     // 起始半径
    public PointF? SpiralCenter { get; set; } = null;        // 螺旋中心
}

// 极坐标转换
r = r₀ + a × θ
x = r × cos(θ)
y = r × sin(θ)
```

### 4. ⚡ ACSPL代码生成器

#### 运动控制代码结构
```acspl
// 1. 系统初始化
int AxX = 0, AxY = 1
real PulseWidth1 = 0.001
lc.Init()
enable(AxX), enable(AxY)

// 2. 坐标数组定义
global real XCoord10(1000), YCoord10(1000)
XCoord10(0) = 12.3456; YCoord10(0) = 67.8910;

// 3. 运动控制指令
lc.CoordinateArrPulse(1000, PulseWidth1, XCoord10, YCoord10)
PTP/ve (AxX,AxY), 12.3456, 67.8910, 50.0
lc.LaserEnable()
PTP/e (AxX,AxY), 15.2341, 71.4567
lc.LaserDisable()
```

#### 可配置参数
- **运动速度**: 移动速度、加工速度独立设置
- **脉冲控制**: 脉冲宽度、周期、额外脉冲数
- **路径优化**: 容差控制、方向变化检测
- **坐标变换**: XY翻转、中心化、旋转角度

## 🏗️ 技术架构

### 分层架构设计

```
┌─────────────────────────────────────┐
│        UI Layer (WinForms)          │
│  MainForm / ConfigForm / Controls   │
├─────────────────────────────────────┤
│       Business Logic Layer         │
│    Interfaces / Services / Models  │
├─────────────────────────────────────┤
│      Data Access Layer             │
│    Parsers / Data Services         │
├─────────────────────────────────────┤
│      Infrastructure Layer          │
│   DxfFast / netDxf / Accord       │
└─────────────────────────────────────┘
```

### 技术栈详解

| 技术组件 | 版本 | 用途 | 优势 |
|---------|------|------|------|
| **.NET 8.0** | Latest | 运行时框架 | 高性能、跨平台 |
| **Windows Forms** | Built-in | 桌面UI框架 | 成熟稳定、快速开发 |
| **自研Rust解析器** | Custom | CAD文件处理 | 专用优化、零拷贝、高性能 |
| **netDxf** | 3.0.0 | 辅助DXF解析 | 开源、轻量级 |
| **Accord.NET** | 3.8.0 | 机器学习算法 | DBSCAN聚类 |
| **Newtonsoft.Json** | 13.0.3 | JSON处理 | 数据序列化 |
| **System.Drawing** | 8.0.0 | 图形处理 | 坐标计算、图形变换 |

---

## 💻 系统要求

### 最低配置
- **操作系统**: Windows 10 x64 (版本 1903 或更高)
- **处理器**: Intel i3-4000 / AMD FX-6000 系列或更高
- **内存**: 4 GB RAM
- **存储空间**: 500 MB 可用磁盘空间
- **图形**: DirectX 11 兼容显卡
- **框架**: .NET 8.0 Runtime (自动安装)

### 推荐配置
- **操作系统**: Windows 11 x64
- **处理器**: Intel i5-8000 / AMD Ryzen 5 3000 系列或更高
- **内存**: 8 GB RAM 或更高
- **存储空间**: 2 GB 可用磁盘空间 (SSD推荐)
- **图形**: 独立显卡 (大型DXF文件预览)

### 开发环境
- **IDE**: Visual Studio 2022 (17.8.0 或更高)
- **SDK**: .NET 8.0 SDK
- **工具**: Git, NuGet Package Manager
- **调试**: Windows SDK (10.0.22621.0 或更高)

---

## 🔧 安装部署

### 快速安装 (推荐)

1. **下载发布版本**
   ```bash
   # 下载最新发布版本
   https://github.com/your-repo/dxf2-acspl/releases/latest
   ```

2. **解压并运行**
   ```bash
   # 解压到目标目录
   unzip DXFtoACSPL-v2.0-win-x64.zip
   
   # 运行主程序
   ./DXFtoACSPL.WinForms.exe
   ```

### 源码编译部署

1. **克隆项目**
   ```bash
   git clone https://github.com/your-repo/dxf2-acspl.git
   cd dxf2-acspl
   ```

2. **还原依赖包**
   ```bash
   dotnet restore DXFtoACSPL.sln
   ```

3. **编译项目**
   ```bash
   # Debug版本
   dotnet build DXFtoACSPL.sln --configuration Debug
   
   # Release版本
   dotnet build DXFtoACSPL.sln --configuration Release
   ```

4. **运行程序**
   ```bash
   cd DXFtoACSPL.WinForms
   dotnet run
   ```

### 发布部署

```bash
# 发布为单文件可执行程序
dotnet publish DXFtoACSPL.WinForms/DXFtoACSPL.WinForms.csproj \
  --configuration Release \
  --runtime win-x64 \
  --self-contained true \
  --single-file \
  --output ./publish
```

### Rust解析器配置

项目依赖以下自研Rust解析器组件，需确保DLL文件位于正确路径：

```
libs/
└── dxf_fast_ffi.dll     # Rust解析器FFI库
```

---

## 📖 用户指南

### 基本工作流程

```
加载DXF文件 → 配置处理参数 → 解析圆形实体 → 选择路径算法 → 生成优化路径 → 生成ACSPL代码 → 导出结果文件
```

### 详细操作步骤

#### 1. 启动应用程序
- 双击 `DXFtoACSPL.WinForms.exe` 启动程序
- 程序将自动检测Rust解析器组件可用性
- 界面加载完成后显示主工作区

#### 2. 加载DXF文件
```
工具栏 → "打开DXF文件" 按钮 → 选择DXF文件
```
- 支持的文件格式: `.dxf`, `.dwg`
- 文件大小限制: 建议 < 100MB
- 加载成功后显示文件信息和实体统计

#### 3. 配置处理参数

##### 圆形检测参数
- **圆形检测容差**: `1.5mm` (多段线圆形识别精度)
- **中心点容差**: `0.1mm` (重复圆形去除精度)  
- **最小半径**: `0.1mm` (过滤小圆形)
- **最大半径**: `100mm` (过滤大圆形)

##### 运动控制参数
- **移动速度**: `50mm/s` (空程移动速度)
- **加工速度**: `20mm/s` (激光加工速度)
- **脉冲周期**: `0.1s` (激光脉冲周期)
- **额外脉冲**: `0` (每个点的额外脉冲数)

##### 路径规划参数
- **路径算法**: 选择路径规划算法
- **容差1**: `10.0mm` (蛇形路径行分组)
- **容差2**: `1000.0mm` (空间聚类距离)

#### 4. 解析圆形实体
```
操作区 → "解析圆形" 按钮
```
- 实时显示解析进度
- 显示检测到的圆形数量
- 在预览区显示圆形分布图

#### 5. 生成优化路径
```
操作区 → "生成路径" 按钮
```
- 根据选择的算法生成路径
- 实时显示路径优化过程
- 在路径预览区显示生成的路径

#### 6. 生成ACSPL代码
```
操作区 → "生成代码" 按钮  
```
- 自动生成标准ACSPL代码
- 代码编辑器中显示生成结果
- 提供语法高亮和格式化

#### 7. 导出结果
```
文件菜单 → "导出" → 选择格式
```
- **ACSPL代码**: `.prg` 文件
- **路径坐标**: `.json` / `.csv` 文件
- **SVG预览**: `.svg` 图形文件

---

## 📞 技术支持

### 常见问题解答

#### Q: Rust解析器组件加载问题如何处理？
A: 请确保以下DLL文件位于程序目录的`libs`文件夹中：
```
libs/
└── dxf_fast_ffi.dll
```

#### Q: 大型DXF文件处理缓慢怎么办？
A: 建议采用以下优化策略：
1. 调整`CircleDetectionTolerance`参数减少检测精度
2. 使用`MinRadius`和`MaxRadius`过滤不需要的圆形
3. 选择性能更高的路径算法(如螺旋填充)
4. 增加系统内存配置

#### Q: ACSPL代码在设备上运行异常？
A: 请检查以下配置：
1. 确认设备支持的ACSPL语法版本
2. 检查坐标单位是否匹配(mm/inch)
3. 验证运动速度参数是否在设备允许范围内
4. 确认激光控制指令格式正确

### 联系方式

- **技术支持邮箱**: support@your-company.com
- **开发团队**: dev-team@your-company.com
- **GitHub Issues**: [https://github.com/your-repo/dxf2-acspl/issues](https://github.com/your-repo/dxf2-acspl/issues)
- **开发文档**: [https://docs.your-company.com/dxf2-acspl](https://docs.your-company.com/dxf2-acspl)

---

## 📄 许可证

本项目基于专有许可证开发，版权归**无锡光子芯片研究院**所有。

- 项目集成了自研Rust解析器和开源netDxf组件
- 仅限内部开发和学习使用
- 不得用于商业分发或二次开发销售
- 详细许可条款请参考 [LICENSE](LICENSE) 文件

---

<div align="center">

**DXF to ACSPL Converter v2.0**  
*专业级激光加工路径规划解决方案*

Made with ❤️ by 无锡光子芯片研究院开发团队

[⭐ 给个Star](https://github.com/your-repo/dxf2-acspl) |
[📖 查看文档](https://docs.your-company.com) |
[🐛 报告问题](https://github.com/your-repo/dxf2-acspl/issues) |
[💬 技术讨论](https://github.com/your-repo/dxf2-acspl/discussions)

</div>