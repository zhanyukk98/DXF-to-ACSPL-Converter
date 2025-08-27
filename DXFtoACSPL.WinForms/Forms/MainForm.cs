using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.IO;

using DXFtoACSPL.Core.Interfaces;
using DXFtoACSPL.Core.Models;
using DXFtoACSPL.Core.Services;
using DXFtoACSPL.Core.Parsers;
using DXFtoACSPL.WinForms.Controls;
using PathElement = DXFtoACSPL.Core.Services.PathGenerator.PathElement; // 使用PathGenerator中的PathElement类型

namespace DXFtoACSPL.WinForms.Forms;

public partial class MainForm : Form
{

    
    private readonly IDxfParser _dxfParser;
    private readonly IDataService _dataService;
    private ProcessingConfig _config;
    private DXFtoACSPL.WinForms.Controls.Direct2DDxfPreviewControl? _dxfPreviewControl;
    private CirclesDataGridView? _circlesDataGridView;
    private DXFtoACSPL.WinForms.Controls.Direct2DPathVisualizationControl? _pathPreviewControl;
    private List<CircleEntity> _circles = new();
    private List<PathElement> _pathElements = new(); // 添加路径元素列表
    private RectangleF _modelBounds = RectangleF.Empty;

    // UI控件
    private Label? _lblEntityCount;
    private Label? _lblHolePositions;
    private Label? _lblPathPoints;
    private Label? _lblProcessingStatus;
    private TextBox? _txtDxfPath;
    private TextBox? _txtCircleTolerance;
    private TextBox? _txtCenterTolerance;
    private TextBox? _txtMinRadius;
    private TextBox? _txtMaxRadius;
    private TextBox? _txtScale;
    private TextBox? _txtMoveVelocity;
    private TextBox? _txtProcessVelocity;
    private TextBox? _txtExtraPulses;
    private TextBox? _txtPulsePeriod;
    private TextBox? _txtPathTolerance1;
    private TextBox? _txtPathTolerance2;
    private CheckBox? _chkCentering;
    private TabControl? _mainTabControl;
    private Button? _generatePathButton;
    private Button? _convertButton;
    private TextBox? _txtACSPLCode; // 新增：ACSPL代码文本框
    private ProgressBar? _progressBar; // 新增：进度条
    private TextBox? _logTextBox; // 新增：日志文本框
    private DateTime _operationStartTime; // 新增：操作开始时间
    private Label? _lblRotationAngle; // 新增：加工旋转角度标签
    private TextBox? _txtRotationAngle; // 新增：加工旋转角度输入框
    private ComboBox? _cboPathAlgorithm; // 新增：路径算法选择
    private Label? _lblPathAlgorithm; // 新增：路径算法标签
    private string _debugInfo = string.Empty; // 新增：调试信息字段
    private string? _startupPath; // 新增：启动参数指定的DXF路径（可选）

    public MainForm()
    {
        try
        {

            
            InitializeComponent();
            
            _dxfParser = new DxfFastAdapter();
            _dataService = new JsonDataService();
            _config = new ProcessingConfig();
            
            InitializeUI();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"MainForm 初始化失败: {ex.Message}\n\n{ex.StackTrace}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            throw;
        }
    }

    // 新增：支持从命令行传入启动文件路径，窗体显示后自动加载
    public MainForm(string? startupPath) : this()
    {
        _startupPath = startupPath;
        if (!string.IsNullOrWhiteSpace(_startupPath))
        {
            this.Shown += async (s, e) =>
            {
                try
                {
                    var filePath = _startupPath!;
                    if (!Path.IsPathRooted(filePath))
                        filePath = Path.GetFullPath(filePath);

                    if (!File.Exists(filePath))
                    {
                        MessageBox.Show($"启动参数指定的文件不存在:\n{filePath}", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    if (_txtDxfPath != null) _txtDxfPath.Text = filePath;
                    AddLog($"通过命令行参数自动加载: {filePath}");
                    await LoadDxfFile(filePath);
                }
                catch (Exception ex)
                {
                    AddLog($"通过命令行自动加载失败: {ex.Message}");
                    MessageBox.Show($"通过命令行自动加载失败:\n{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
        }
    }
    private void InitializeUI()
    {
        this.Text = "DXF to ACSPL Converter";
        this.Size = new Size(1400, 900);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.MinimumSize = new Size(1200, 800);
        this.BackColor = Color.White;

        CreateControls();
    }

    private void CreateControls()
    {
        // 主容器
        var mainContainer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White
        };

        // 左侧控制面板 - 固定宽度350
        var leftPanel = new Panel
        {
            Dock = DockStyle.Left,
            Width = 350,
            BackColor = Color.White,
            Padding = new Padding(15)
        };

        // 右侧内容面板 - 填充剩余空间
        var rightPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(10)
        };

        // 创建左侧控制面板内容
        CreateLeftPanel(leftPanel);

        // 创建右侧内容面板
        CreateRightPanel(rightPanel);

        // 添加面板到主容器
        mainContainer.Controls.Add(rightPanel);
        mainContainer.Controls.Add(leftPanel);

        this.Controls.Add(mainContainer);
    }

    private void CreateLeftPanel(Panel leftPanel)
    {
        leftPanel.BackColor = Color.FromArgb(248, 248, 248);
        leftPanel.Padding = new Padding(10);
        leftPanel.AutoScroll = true;

        // 创建滚动面板
        var scrollPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.White
        };

        // 应用标题和Logo
        var headerPanel = CreateHeaderPanel();
        scrollPanel.Controls.Add(headerPanel);

        // 文件操作卡片
        var fileOperationsCard = CreateFileOperationsCard();
        fileOperationsCard.Location = new Point(0, headerPanel.Height + 5); // 减少间距
        scrollPanel.Controls.Add(fileOperationsCard);

        // 处理状态卡片
        var statusCard = CreateStatusCard();
        statusCard.Location = new Point(0, headerPanel.Height + fileOperationsCard.Height + 10); // 减少间距
        scrollPanel.Controls.Add(statusCard);

        // 圆形检测参数卡片
        var paramsCard = CreateParametersCard();
        paramsCard.Location = new Point(0, headerPanel.Height + fileOperationsCard.Height + statusCard.Height + 15); // 减少间距
        scrollPanel.Controls.Add(paramsCard);

        leftPanel.Controls.Add(scrollPanel);
    }

    private Panel CreateHeaderPanel()
    {
        var headerPanel = new Panel
        {
            Height = 80,
            Dock = DockStyle.Top,
            BackColor = Color.White
        };

        // Logo和标题
        var logoLabel = new Label
        {
            Text = "W",
            Font = new Font("Arial", 24, FontStyle.Bold),
            ForeColor = Color.Red,
            Location = new Point(10, 10),
            Size = new Size(40, 40),
            TextAlign = ContentAlignment.MiddleCenter
        };

        var titleLabel = new Label
        {
            Text = "DXF to ACSPL Converter",
            Font = new Font("Microsoft YaHei UI", 16, FontStyle.Bold),
            ForeColor = Color.FromArgb(64, 64, 64),
            Location = new Point(60, 10),
            Size = new Size(250, 25),
            TextAlign = ContentAlignment.MiddleLeft
        };

        var subtitleLabel = new Label
        {
            Text = "专业的DXF文件转换工具",
            Font = new Font("Microsoft YaHei UI", 10),
            ForeColor = Color.Gray,
            Location = new Point(60, 35),
            Size = new Size(250, 20),
            TextAlign = ContentAlignment.MiddleLeft
        };

        headerPanel.Controls.AddRange(new Control[] { logoLabel, titleLabel, subtitleLabel });
        return headerPanel;
    }

    private Panel CreateFileOperationsCard()
    {
        var card = CreateCardPanel("📁 文件操作", 220); // 增加高度从200到220，确保按钮完整显示

        // DXF输入文件
        var fileLabel = new Label
        {
            Text = "DXF输入文件:",
            Font = new Font("Microsoft YaHei UI", 9),
            ForeColor = Color.FromArgb(64, 64, 64),
            Location = new Point(15, 35),
            Size = new Size(100, 20)
        };

        _txtDxfPath = new TextBox
        {
            Text = "选择或输入DXF文件路径",
            Font = new Font("Microsoft YaHei UI", 9),
            Location = new Point(15, 55),
            Size = new Size(200, 25),
            ReadOnly = true,
            BackColor = Color.FromArgb(248, 248, 248)
        };

        var browseButton = new Button
        {
            Text = "浏览",
            Font = new Font("Microsoft YaHei UI", 9),
            Location = new Point(225, 55),
            Size = new Size(60, 25),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(240, 240, 240),
            ForeColor = Color.FromArgb(64, 64, 64)
        };
        browseButton.Click += OnBrowseFile;

        // 操作按钮
        var loadButton = CreateModernButton("加载DXF文件", OnLoadDxfFile, Color.FromArgb(0, 120, 215));
        loadButton.Location = new Point(15, 90);
        loadButton.Size = new Size(270, 35);

        _generatePathButton = CreateModernButton("加工路径生成", OnGeneratePath, Color.FromArgb(255, 140, 0));
        _generatePathButton.Location = new Point(15, 130);
        _generatePathButton.Size = new Size(270, 35);
        _generatePathButton.Enabled = false;

        // 转换按钮
        _convertButton = CreateModernButton("ACSPL代码生成", OnConvertToAcspl, Color.FromArgb(220, 53, 69));
        _convertButton.Location = new Point(15, 170);
        _convertButton.Size = new Size(270, 35);
        _convertButton.Enabled = false; // 初始禁用

        card.Controls.AddRange(new Control[] { 
            fileLabel, _txtDxfPath, browseButton, 
            loadButton, _generatePathButton, _convertButton
        });

        return card;
    }

    private Panel CreateStatusCard()
    {
        var card = CreateCardPanel("📊 处理状态", 120);

        _lblEntityCount = CreateStatusLabel("实体数量:", "-", 35);
        _lblHolePositions = CreateStatusLabel("检测孔位:", "-", 55);
        _lblPathPoints = CreateStatusLabel("路径点数:", "-", 75);
        _lblProcessingStatus = CreateStatusLabel("处理状态:", "未开始", 95);

        card.Controls.AddRange(new Control[] { 
            _lblEntityCount, _lblHolePositions, _lblPathPoints, _lblProcessingStatus 
        });

        return card;
    }

    private Panel CreateParametersCard()
    {
        var card = CreateCardPanel("⚙️ 路径与加工参数", 480);

        int y = 35;
        int dy = 32;
        int labelWidth = 120;
        int inputWidth = 80;

        // 圆形检测容差
        card.Controls.Add(CreateParamLabel("圆形检测容差:", 15, y, labelWidth));
        _txtCircleTolerance = CreateParamTextBox(_config.CircleDetectionTolerance.ToString(), 140, y, inputWidth, (s, e) => UpdateConfigFromUI());
        card.Controls.Add(_txtCircleTolerance);
        y += dy;

        // 中心点去重容差
        card.Controls.Add(CreateParamLabel("中心点容差:", 15, y, labelWidth));
        _txtCenterTolerance = CreateParamTextBox(_config.CenterPointTolerance.ToString(), 140, y, inputWidth, (s, e) => UpdateConfigFromUI());
        card.Controls.Add(_txtCenterTolerance);
        y += dy;

        // 最小半径
        card.Controls.Add(CreateParamLabel("最小半径:", 15, y, labelWidth));
        _txtMinRadius = CreateParamTextBox(_config.MinRadius.ToString(), 140, y, inputWidth, (s, e) => UpdateConfigFromUI());
        card.Controls.Add(_txtMinRadius);
        y += dy;

        // 最大半径
        card.Controls.Add(CreateParamLabel("最大半径:", 15, y, labelWidth));
        _txtMaxRadius = CreateParamTextBox(_config.MaxRadius.ToString(), 140, y, inputWidth, (s, e) => UpdateConfigFromUI());
        card.Controls.Add(_txtMaxRadius);
        y += dy;

        // 缩放比例
        card.Controls.Add(CreateParamLabel("缩放比例:", 15, y, labelWidth));
        _txtScale = CreateParamTextBox(_config.Scale.ToString(), 140, y, inputWidth, (s, e) => UpdateConfigFromUI());
        card.Controls.Add(_txtScale);
        y += dy;

        // 移动速度
        card.Controls.Add(CreateParamLabel("移动速度:", 15, y, labelWidth));
        _txtMoveVelocity = CreateParamTextBox(_config.MoveVelocity.ToString(), 140, y, inputWidth, (s, e) => UpdateConfigFromUI());
        card.Controls.Add(_txtMoveVelocity);
        y += dy;

        // 加工速度
        card.Controls.Add(CreateParamLabel("加工速度:", 15, y, labelWidth));
        _txtProcessVelocity = CreateParamTextBox(_config.ProcessVelocity.ToString(), 140, y, inputWidth, (s, e) => UpdateConfigFromUI());
        card.Controls.Add(_txtProcessVelocity);
        y += dy;

        // 额外脉冲数
        card.Controls.Add(CreateParamLabel("额外脉冲数:", 15, y, labelWidth));
        _txtExtraPulses = CreateParamTextBox(_config.ExtraPulses.ToString(), 140, y, inputWidth, (s, e) => UpdateConfigFromUI());
        card.Controls.Add(_txtExtraPulses);
        y += dy;

        // 脉冲周期
        card.Controls.Add(CreateParamLabel("脉冲周期:", 15, y, labelWidth));
        _txtPulsePeriod = CreateParamTextBox(_config.PulsePeriod.ToString(), 140, y, inputWidth, (s, e) => UpdateConfigFromUI());
        card.Controls.Add(_txtPulsePeriod);
        y += dy;

        // 路径规划容差1
        card.Controls.Add(CreateParamLabel("路径容差1:", 15, y, labelWidth));
        _txtPathTolerance1 = CreateParamTextBox(_config.PathTolerance1.ToString(), 140, y, inputWidth, (s, e) => UpdateConfigFromUI());
        card.Controls.Add(_txtPathTolerance1);
        y += dy;

        // 路径规划容差2
        card.Controls.Add(CreateParamLabel("路径容差2:", 15, y, labelWidth));
        _txtPathTolerance2 = CreateParamTextBox(_config.PathTolerance2.ToString(), 140, y, inputWidth, (s, e) => UpdateConfigFromUI());
        card.Controls.Add(_txtPathTolerance2);
        y += dy;

        // 加工旋转角度
        _lblRotationAngle = new Label { Text = "加工旋转角度(度)", Left = 15, Top = y, Width = 120 };
        _txtRotationAngle = CreateParamTextBox(_config.RotationAngle.ToString(), 140, y, inputWidth, (s, e) => UpdateConfigFromUI());
        card.Controls.Add(_lblRotationAngle);
        card.Controls.Add(_txtRotationAngle);
        y += 32;

        // 路径算法选择
        _lblPathAlgorithm = new Label { Text = "路径生成算法:", Left = 15, Top = y, Width = 120 };
        _cboPathAlgorithm = new ComboBox
        {
            Location = new Point(140, y),
            Size = new Size(120, 25),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Microsoft YaHei UI", 9F)
        };
        _cboPathAlgorithm.Items.AddRange(new object[]
        {
            "聚类算法",
            "螺旋填充算法", 
            "蛇形路径算法",
            "最近邻算法",
            "测试算法",
            "聚类算法强化版"
        });
        _cboPathAlgorithm.SelectedIndex = 0;
        _cboPathAlgorithm.SelectedIndexChanged += (s, e) => UpdateConfigFromUI();
        card.Controls.Add(_lblPathAlgorithm);
        card.Controls.Add(_cboPathAlgorithm);
        y += 32;

        // 是否启用中心化
        _chkCentering = new CheckBox { Text = "启用中心化", Left = 15, Top = y, Width = 120, Checked = _config.EnableCentering };
        _chkCentering.CheckedChanged += (s, e) => { _config.EnableCentering = _chkCentering.Checked; };
        card.Controls.Add(_chkCentering);
        y += dy;

        return card;
    }

    private Label CreateParamLabel(string text, int x, int y, int width)
    {
        return new Label { Text = text, Left = x, Top = y, Width = width, Font = new Font("Microsoft YaHei UI", 9), ForeColor = Color.FromArgb(64, 64, 64) };
    }

    private TextBox CreateParamTextBox(string text, int x, int y, int width, EventHandler? onChanged)
    {
        var txt = new TextBox { Text = text, Left = x, Top = y, Width = width, Font = new Font("Microsoft YaHei UI", 9) };
        if (onChanged != null) txt.TextChanged += onChanged;
        return txt;
    }

    private Panel CreateCardPanel(string title, int height)
    {
        var card = new Panel
        {
            Size = new Size(320, height),
            BackColor = Color.White,
            BorderStyle = BorderStyle.None
        };

        // 标题
        var titleLabel = new Label
        {
            Text = title,
            Font = new Font("Microsoft YaHei UI", 11, FontStyle.Bold),
            ForeColor = Color.FromArgb(64, 64, 64),
            Location = new Point(15, 10),
            Size = new Size(200, 20)
        };

        card.Controls.Add(titleLabel);
        return card;
    }

    private Label CreateStatusLabel(string label, string value, int y)
    {
        return new Label
        {
            Text = $"{label} {value}",
            Font = new Font("Microsoft YaHei UI", 9),
            ForeColor = Color.FromArgb(64, 64, 64),
            Location = new Point(15, y),
            Size = new Size(280, 20)
        };
    }

    private Button CreateModernButton(string text, EventHandler clickHandler, Color color)
    {
        var button = new Button
        {
            Text = text,
            Font = new Font("Microsoft YaHei UI", 9),
            ForeColor = Color.White,
            BackColor = color,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };

        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(
            Math.Max(0, color.R - 20), 
            Math.Max(0, color.G - 20), 
            Math.Max(0, color.B - 20)
        );

        button.Click += clickHandler;
        return button;
    }

    private void CreateRightPanel(Panel rightPanel)
    {
        rightPanel.BackColor = Color.White;
        rightPanel.Padding = new Padding(10);

        // 创建主内容面板
        var contentPanel = new Panel
        {
            Dock = DockStyle.Fill
        };

        // 创建选项卡控件
        _mainTabControl = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = new Font("Microsoft YaHei UI", 9),
            BackColor = Color.White
        };

        // DXF预览选项卡
        var previewTab = new TabPage("🖼️ DXF预览");
        previewTab.BackColor = Color.White;
        _dxfPreviewControl = new DXFtoACSPL.WinForms.Controls.Direct2DDxfPreviewControl();
        _dxfPreviewControl.Dock = DockStyle.Fill;
        _dxfPreviewControl.CircleSelected += (circle) => OnCircleSelected(circle);
        previewTab.Controls.Add(_dxfPreviewControl);
        _mainTabControl.TabPages.Add(previewTab);

        // 实体列表选项卡
        var entityListTab = new TabPage("📋 实体列表");
        entityListTab.BackColor = Color.White;
        _circlesDataGridView = new DXFtoACSPL.WinForms.Controls.CirclesDataGridView();
        _circlesDataGridView.Dock = DockStyle.Fill;
        entityListTab.Controls.Add(_circlesDataGridView);
        _mainTabControl.TabPages.Add(entityListTab);

        // 加工路径图示页签
        var pathVisualizationTab = new TabPage("🛤️ 加工路径图示");
        var pathVisualizationPanel = new Panel { Dock = DockStyle.Fill };
        
        // 添加路径坐标按钮
        var pathCoordinatesButton = new Button
        {
            Text = "路径坐标",
            Size = new Size(100, 30),
            Location = new Point(10, 10),
            BackColor = Color.FromArgb(0, 122, 204),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Microsoft YaHei UI", 9, FontStyle.Bold)
        };
        pathCoordinatesButton.Click += OnPathCoordinates;
        pathVisualizationPanel.Controls.Add(pathCoordinatesButton);
        
        _pathPreviewControl = new DXFtoACSPL.WinForms.Controls.Direct2DPathVisualizationControl();
        _pathPreviewControl.Dock = DockStyle.Fill;
        _pathPreviewControl.LogMessage += (message) => OnPreviewLogMessage(message); // 修复事件连接
        pathVisualizationPanel.Controls.Add(_pathPreviewControl);
        pathVisualizationTab.Controls.Add(pathVisualizationPanel);
        _mainTabControl.TabPages.Add(pathVisualizationTab);

        // ACSPL代码页签
        var acsplCodeTab = new TabPage("💻 ACSPL代码");
        var acsplCodePanel = new Panel { Dock = DockStyle.Fill };
        
        // 添加工具栏
        var acsplToolStrip = new ToolStrip();
        var copyCodeButton = new ToolStripButton("复制代码", null, OnCopyACSPLCode);
        var saveCodeButton = new ToolStripButton("保存代码", null, OnSaveACSPLCode);
        acsplToolStrip.Items.Add(copyCodeButton);
        acsplToolStrip.Items.Add(saveCodeButton);
        
        // 添加代码文本框
        _txtACSPLCode = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            Font = new Font("Consolas", 10),
            ReadOnly = true,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.LightGreen,
            WordWrap = false
        };
        
        acsplCodePanel.Controls.Add(acsplToolStrip);
        acsplCodePanel.Controls.Add(_txtACSPLCode);
        acsplToolStrip.Dock = DockStyle.Top;
        _txtACSPLCode.Dock = DockStyle.Fill;
        acsplCodeTab.Controls.Add(acsplCodePanel);
        _mainTabControl.TabPages.Add(acsplCodeTab);

        // 处理日志选项卡
        var logTab = new TabPage("📋 处理日志");
        logTab.BackColor = Color.White;
        
        // 创建日志文本框
        _logTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            ReadOnly = true,
            Font = new Font("Consolas", 9),
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.LightGreen,
            WordWrap = false
        };
        
        // 创建日志工具栏
        var logToolStrip = new ToolStrip();
        var clearLogButton = new ToolStripButton("清空日志", null, OnClearLog);
        var saveLogButton = new ToolStripButton("保存日志", null, OnSaveLog);
        logToolStrip.Items.Add(clearLogButton);
        logToolStrip.Items.Add(saveLogButton);
        
        // 布局日志页签
        var logPanel = new Panel { Dock = DockStyle.Fill };
        logPanel.Controls.Add(logToolStrip);
        logPanel.Controls.Add(_logTextBox);
        logToolStrip.Dock = DockStyle.Top;
        _logTextBox.Dock = DockStyle.Fill;
        logTab.Controls.Add(logPanel);
        
        _mainTabControl.TabPages.Add(logTab);

        // 组装主内容面板
        contentPanel.Controls.Add(_mainTabControl);

        // 创建底部面板 - 包含进度条和版权信息
        var bottomPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 17, // 减少高度从50到17（约三分之一）
            BackColor = Color.Transparent
        };

        // 创建美化的进度条 - 放在底部左侧
        _progressBar = new ProgressBar
        {
            Location = new Point(10, 5), // 调整位置从15到5
            Size = new Size(200, 6), // 稍微调整高度从8到6
            Style = ProgressBarStyle.Continuous,
            Visible = false,
            BackColor = Color.FromArgb(240, 240, 240),
            ForeColor = Color.FromArgb(0, 122, 204)
        };

        // 创建进度条标签
        var progressLabel = new Label
        {
            Location = new Point(220, 2), // 调整位置从3到2
            Size = new Size(150, 12), // 调整高度从10到12
            Font = new Font("Microsoft YaHei UI", 6), // 减小字体从7到6
            ForeColor = Color.FromArgb(64, 64, 64),
            Text = "就绪"
        };

        // 版权信息 - 放在底部右侧
        var copyrightLabel = new Label
        {
            Text = "无锡光子芯片研究院",
            Font = new Font("Microsoft YaHei UI", 7), // 稍微减小字体从8到7
            ForeColor = Color.Gray,
            TextAlign = ContentAlignment.MiddleRight,
            Dock = DockStyle.Right,
            Width = 150
        };

        // 组装底部面板
        bottomPanel.Controls.Add(_progressBar);
        bottomPanel.Controls.Add(progressLabel);
        bottomPanel.Controls.Add(copyrightLabel);

        // 组装右侧面板
        rightPanel.Controls.Add(contentPanel);
        rightPanel.Controls.Add(bottomPanel);
    }

    // 事件处理方法
    private void OnBrowseFile(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "DXF文件|*.dxf|所有文件|*.*",
            Title = "选择DXF文件"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _txtDxfPath!.Text = dialog.FileName;
        }
    }

    private async void OnLoadDxfFile(object? sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_txtDxfPath?.Text) || _txtDxfPath.Text == "选择或输入DXF文件路径")
        {
            MessageBox.Show("请先选择DXF文件", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            ShowProgress("正在加载DXF文件...");
            StartOperationLog("加载DXF文件");
            
            await LoadDxfFile(_txtDxfPath.Text);
            
            EndOperationLog("加载DXF文件");
            HideProgress();
        }
        catch (Exception ex)
        {
            AddLog($"DXF文件加载失败: {ex.Message}");
            HideProgress();
            MessageBox.Show($"加载DXF文件失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // 新增：重新加载DXF文件以应用新的旋转角度
    private async Task ReloadDxfWithRotation()
    {
        if (string.IsNullOrEmpty(_txtDxfPath?.Text) || _txtDxfPath.Text == "选择或输入DXF文件路径")
        {
            return;
        }

        try
        {
            AddLog($"重新加载DXF文件，应用旋转角度: {_config.RotationAngle}度");
            
            // 清理之前的数据
            if (_dxfPreviewControl != null)
            {
                _dxfPreviewControl.Clear();
            }
            
            // 强制垃圾回收
            GC.Collect();
            GC.WaitForPendingFinalizers();
            
            // 重新解析圆形实体
            await ParseCircles();
            
            AddLog($"重新加载完成，共提取 {_circles.Count} 个圆形实体");
            
            // 更新状态
            UpdateProcessingStatus($"DXF文件重新加载完成，旋转角度: {_config.RotationAngle}度");
        }
        catch (Exception ex)
        {
            AddLog($"重新加载DXF文件失败: {ex.Message}");
            MessageBox.Show($"重新加载DXF文件失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnConvertToAcspl(object? sender, EventArgs e)
    {
        try
        {
            // 检查是否已生成路径
            if (_pathElements.Count == 0)
            {
                MessageBox.Show("请先生成加工路径！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ShowProgress("正在生成ACSPL代码...");
            StartOperationLog("生成ACSPL代码");

            // 使用ACSPL代码生成器
            var codeGenerator = new DXFtoACSPL.Core.Services.ACSPLCodeGenerator();
            var acsplCode = codeGenerator.GenerateACSPLCode(_pathElements, _config);

            // 显示在ACSPL代码页签中
            if (_txtACSPLCode != null)
            {
                _txtACSPLCode.Text = acsplCode;
                
                // 切换到ACSPL代码页签
                if (_mainTabControl != null)
                {
                    _mainTabControl.SelectedIndex = 3; // ACSPL代码页签索引
                }
            }

            EndOperationLog("生成ACSPL代码");
            UpdateProcessingStatus("ACSPL代码生成完成");
            HideProgress();
        }
        catch (Exception ex)
        {
            AddLog($"生成ACSPL代码失败: {ex.Message}");
            HideProgress();
            MessageBox.Show($"生成ACSPL代码失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            UpdateProcessingStatus("ACSPL代码生成失败");
        }
    }

    private async void OnGeneratePath(object? sender, EventArgs e)
    {
        try
        {
            StartOperationLog("生成加工路径");
            _generatePathButton!.Enabled = false;
            
            UpdateProgress(0, 100);
            ShowProgress("正在生成加工路径...");
            
            // 清空原有路径数据
            _pathElements.Clear();
            _pathPreviewControl?.ClearPathData();
            
            // 更新配置
            UpdateConfigFromUI();
            
            // 添加调试信息，确认配置更新
            _debugInfo += $"\nOnGeneratePath: 更新配置后的旋转角度 = {_config.RotationAngle}度";
            _debugInfo += $"\nOnGeneratePath: 文本框中的旋转角度 = {_txtRotationAngle?.Text ?? "空"}";
            
            UpdateProgress(20, 100);
            
            // 生成路径元素
            var pathElements = await Task.Run(() => GeneratePathElements());
            
            UpdateProgress(80, 100);
            
            // 添加调试信息，确认传递给路径预览控件的数据
            if (pathElements.Count > 0)
            {
                var firstPoint = pathElements.FirstOrDefault(p => p.Type == "Point");
                if (firstPoint?.Data is PointF point)
                {
                    _debugInfo += $"\nOnGeneratePath: 传递给路径预览控件的第一个路径点 = ({point.X:F2}, {point.Y:F2})";
                }
            }
            
            // 保存路径元素
            _pathElements = pathElements;
            
            // 更新路径可视化
            UpdatePathVisualization(pathElements);
            
            UpdateProgress(100, 100);
            
            // 计算实际的聚类数量
            int groupCount = 1; // 默认值
            if (_pathElements.Count > 0)
            {
                // 从第一个路径元素获取聚类数量（如果设置了的话）
                groupCount = _pathElements[0].ClusterCount;
                if (groupCount == 0)
                {
                    // 如果没有设置聚类数量，使用默认计算方式
                    groupCount = _pathElements.Count(p => p.Type == "Marker") + 1;
                }
            }
            
            // 获取调试信息
            var debugInfo = _debugInfo; // 从字段获取调试信息
            
            // 获取路径预览控件的调试信息
            var pathDebugInfo = _pathPreviewControl?.GetPathDebugInfo() ?? "无法获取路径调试信息";

            // 生成旋转后的坐标信息
            var rotationInfo = GenerateRotationInfo();

            // 添加更多调试信息
            var additionalDebugInfo = new List<string>();
            additionalDebugInfo.Add($"=== 完整调试信息 ===");
            additionalDebugInfo.Add($"1. 配置信息:");
            additionalDebugInfo.Add($"   - 旋转角度: {_config.RotationAngle}度");
            additionalDebugInfo.Add($"   - 文本框值: {_txtRotationAngle?.Text ?? "空"}");
            additionalDebugInfo.Add($"");
            additionalDebugInfo.Add($"2. 路径生成信息:");
            additionalDebugInfo.Add($"   - 路径元素总数: {_pathElements.Count}");
            additionalDebugInfo.Add($"   - 路径点数量: {_pathElements.Count(p => p.Type == "Point")}");
            additionalDebugInfo.Add($"   - Marker数量: {_pathElements.Count(p => p.Type == "Marker")}");
            additionalDebugInfo.Add($"");
            additionalDebugInfo.Add($"3. 前5个路径点坐标:");
            var pathPoints = _pathElements.Where(p => p.Type == "Point").Take(5).ToList();
            for (int i = 0; i < pathPoints.Count; i++)
            {
                if (pathPoints[i].Data is PointF point)
                {
                    additionalDebugInfo.Add($"   路径点{i + 1}: ({point.X:F2}, {point.Y:F2})");
                }
            }
            additionalDebugInfo.Add($"");
            additionalDebugInfo.Add($"4. 路径预览控件信息:");
            additionalDebugInfo.Add($"   - 控件是否为空: {_pathPreviewControl == null}");
            additionalDebugInfo.Add($"   - 路径调试信息: {pathDebugInfo}");

            // 显示详细的调试信息
            var message = $"路径生成完成！\n分组数量: {groupCount}\n总路径点数: {_pathElements.Count(p => p.Type == "Point")}\n\n=== 调试信息 ===\n{debugInfo}";
            MessageBox.Show(message, "路径生成结果", MessageBoxButtons.OK, MessageBoxIcon.Information);
            
            _convertButton!.Enabled = true;
            _generatePathButton!.Enabled = true; // 重新启用生成路径按钮
            
            EndOperationLog("生成加工路径");
            HideProgress();
        }
        catch (Exception ex)
        {
            AddLog($"生成加工路径失败: {ex.Message}");
            MessageBox.Show($"生成加工路径失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _generatePathButton!.Enabled = true; // 确保在异常时也重新启用按钮
            HideProgress();
        }
    }
    
    // 新增：生成旋转信息
    private string GenerateRotationInfo()
    {
        var info = new List<string>();
        info.Add($"旋转角度: {_config.RotationAngle}度");
        
        if (_circles.Count > 0)
        {
            info.Add("前5个圆形实体的旋转前后坐标对比:");
            var circlesToShow = _circles.Take(5).ToList();
            
            for (int i = 0; i < circlesToShow.Count; i++)
            {
                var circle = circlesToShow[i];
                var originalPoint = circle.Center;
                var rotatedPoint = RotatePoint(originalPoint, _config.RotationAngle);
                
                info.Add($"圆形{i + 1}:");
                info.Add($"  原始坐标: ({originalPoint.X:F2}, {originalPoint.Y:F2})");
                info.Add($"  旋转后坐标: ({rotatedPoint.X:F2}, {rotatedPoint.Y:F2})");
            }
        }
        
        // 显示路径点的旋转信息
        if (_pathElements.Count > 0)
        {
            info.Add($"\n路径点旋转信息:");
            info.Add($"总路径点数: {_pathElements.Count(p => p.Type == "Point")}");
            
            var pathPoints = _pathElements.Where(p => p.Type == "Point").Take(3).ToList();
            for (int i = 0; i < pathPoints.Count; i++)
            {
                if (pathPoints[i].Data is PointF point)
                {
                    info.Add($"路径点{i + 1}: ({point.X:F2}, {point.Y:F2})");
                }
            }
        }
        
        return string.Join("\n", info);
    }

    /// <summary>
    /// 分析路径生成过程
    /// </summary>
    private void AnalyzePathGeneration()
    {
        try
        {
            // 将PathElement转换为PointF列表（只处理Point类型的元素）
            var pathPoints = _pathElements
                .Where(p => p.Type == "Point" && p.Data is PointF)
                .Select(p => (PointF)p.Data)
                .ToList();
            
            // 创建路径分析器
            var analyzer = new PathAnalyzer();
            
            // 执行分析
            var analysis = analyzer.AnalyzePathGeneration(_circles, pathPoints, _config);
            
            // 添加到日志
            AddLog("=== 路径分析结果 ===");
            AddLog(analysis);
            AddLog("====================");
        }
        catch (Exception ex)
        {
            AddLog($"路径分析失败: {ex.Message}");
        }
    }



    private List<PathElement> GeneratePathElements()
    {
        var pathGenerator = new PathGenerator();
        
        // 收集调试信息
        var debugInfo = new List<string>();
        debugInfo.Add($"路径算法 = {_config.PathAlgorithm}");
        debugInfo.Add($"输入点数 = {_circles.Count}");
        
        // 准备点数据
        var pointDataList = _circles.Select(circle => new PointData(circle.Center)).ToList();
        
        // 使用新的多算法路径生成方法（带调试信息）
        var (result, algorithmDebugInfo) = pathGenerator.GeneratePathWithAlgorithmAndDebug(pointDataList, _config);
        
        // 收集调试信息
        debugInfo.Add($"生成路径点数 = {result.Count}");
        
        // 将调试信息保存到字段中，供弹窗使用
        _debugInfo = string.Join("\n", debugInfo) + "\n" + algorithmDebugInfo;
        
        return result;
    }

    private void UpdatePathVisualization(List<PathElement> pathElements)
    {
        if (_pathPreviewControl != null)
        {
            // 添加调试信息
            if (pathElements.Count > 0)
            {
                var firstPoint = pathElements.FirstOrDefault(p => p.Type == "Point");
                if (firstPoint?.Data is PointF point)
                {
                    _debugInfo += $"\nUpdatePathVisualization: 传递给路径预览控件的第一个路径点 = ({point.X:F2}, {point.Y:F2})";
                }
            }
            
            // 使用新的PathVisualizationControl，直接基于路径坐标生成加工路径图示
                this.Invoke(() =>
                {
                // 优化：预先计算所有旋转后的圆形坐标，避免重复计算
                var rotatedCircles = _circles.Select(circle => new
                {
                    Original = circle,
                    RotatedCenter = RotatePoint(circle.Center, _config.RotationAngle)
                }).ToList();
                
                // 进一步优化：使用空间索引加速查找
                var circleIndex = new Dictionary<PointF, CircleEntity>();
                foreach (var rotatedCircle in rotatedCircles)
                {
                    // 使用四舍五入的坐标作为键，避免浮点数精度问题
                    var key = new PointF(
                        (float)Math.Round(rotatedCircle.RotatedCenter.X * 1000) / 1000,
                        (float)Math.Round(rotatedCircle.RotatedCenter.Y * 1000) / 1000
                    );
                    circleIndex[key] = rotatedCircle.Original;
                }
                
                    // 只提取路径点对应的圆形实体（排除Marker）
                    var pathPointCircles = new List<CircleEntity>();
                    foreach (var pathElement in pathElements)
                    {
                        if (pathElement.Type == "Point" && pathElement.Data is PointF rotatedPoint)
                        {
                        // 首先尝试精确匹配
                        var key = new PointF(
                            (float)Math.Round(rotatedPoint.X * 1000) / 1000,
                            (float)Math.Round(rotatedPoint.Y * 1000) / 1000
                        );
                        
                            CircleEntity? originalCircle = null;
                        if (circleIndex.TryGetValue(key, out var exactMatch))
                        {
                            originalCircle = exactMatch;
                        }
                        else
                        {
                            // 如果没有精确匹配，使用距离查找（但只查找一次）
                            float minDistance = float.MaxValue;
                            foreach (var rotatedCircle in rotatedCircles)
                            {
                                float distance = (float)Math.Sqrt(
                                    Math.Pow(rotatedCircle.RotatedCenter.X - rotatedPoint.X, 2) + 
                                    Math.Pow(rotatedCircle.RotatedCenter.Y - rotatedPoint.Y, 2)
                                );
                                
                                if (distance < minDistance)
                                {
                                    minDistance = distance;
                                    originalCircle = rotatedCircle.Original;
                                }
                                }
                            }
                            
                            if (originalCircle != null)
                            {
                                // 使用原始实体的类型和半径，但使用旋转后的坐标
                                var pathCircle = new CircleEntity
                                {
                                    Index = pathPointCircles.Count + 1,
                                    Center = rotatedPoint, // 使用旋转后的坐标
                                    Radius = originalCircle.Radius, // 使用原始半径
                                    EntityType = originalCircle.EntityType // 使用原始实体类型
                                };
                                pathPointCircles.Add(pathCircle);
                            }
                        }
                    }
                    
                // 使用新的PathVisualizationControl的API，直接设置路径数据
                _pathPreviewControl.SetPathData(pathElements, pathPointCircles);
                    
                    // 切换到加工路径图示页签
                    if (_mainTabControl != null)
                    {
                        _mainTabControl.SelectedIndex = 2; // 加工路径图示页签索引
                    }
            });
        }
    }

    private void EnableButtonsAfterLoad()
    {
        if (_generatePathButton != null)
            _generatePathButton.Enabled = true;
        
        // ACSPL代码生成按钮需要先生成路径才能启用
        if (_convertButton != null)
            _convertButton.Enabled = false; // 初始禁用，需要先生成路径
    }

    private void OnRefreshPreview(object? sender, EventArgs e)
    {
        if (_circles.Count > 0)
        {
            _mainTabControl?.SelectTab(0); // 切换到DXF预览标签页
        }
    }

    private async Task LoadDxfFile(string filePath)
    {
        try
        {
            AddLog("🔍🔍🔍🔍🔍🔍🔍🔍🔍🔍🔍🔍🔍🔍🔍🔍🔍🔍🔍🔍");
            AddLog("🔍 LoadDxfFile方法被调用了！");
            AddLog($"📋 当前使用的解析器: {_dxfParser.GetType().Name} (DxfFast.dll)");
            AddLog("🔍🔍🔍🔍🔍🔍🔍🔍🔍🔍🔍🔍🔍🔍🔍🔍🔍🔍🔍🔍");
            AddLog($"正在加载DXF文件: {Path.GetFileName(filePath)}");
            
            // 清理之前的数据
            if (_dxfPreviewControl != null)
            {
                AddLog("清理预览控件数据...");
                _dxfPreviewControl.Clear();
            }
            
            // 强制垃圾回收，释放内存
            GC.Collect();
            GC.WaitForPendingFinalizers();
            
            AddLog("SharpGL 预览控件已准备就绪，等待圆形数据...");

            // 使用 DxfFast 解析文件（供后续生成路径等使用）
            AddLog("开始通过 DxfFast 解析 DXF 文件...");
            await _dxfParser.LoadFileAsync(filePath);
            AddLog("DxfFast 解析完成");
            
            AddLog("DXF文件加载成功，开始解析实体...");
            
            // 解析圆形实体
            await ParseCircles();
            
            AddLog($"DXF文件处理完成，共提取 {_circles.Count} 个圆形实体");
            
            // 启用相关按钮
            EnableButtonsAfterLoad();
            
            // 更新状态
            UpdateProcessingStatus("DXF文件加载完成");
            AddLog("🔍🔍🔍🔍🔍🔍🔍🔍🔍🔍🔍🔍🔍🔍🔍🔍🔍🔍🔍🔍");
            AddLog("🔍 LoadDxfFile方法结束了！");
            AddLog("🔍🔍🔍🔍🔍🔍🔍🔍🔍🔍🔍🔍🔍🔍🔍🔍🔍🔍🔍🔍");
        }
        catch (Exception ex)
        {
            AddLog($"DXF文件加载失败: {ex.Message}");
            throw;
        }
    }



    private async Task ParseCircles()
    {
        try
        {
            UpdateProcessingStatus("正在解析圆形实体...");
            
            // 使用解析器获取圆形实体（DxfFast）
            _circles = await _dxfParser.ParseCirclesAsync(_config);
            
            // 更新表格数据
            _circlesDataGridView?.SetCircles(_circles);
            
            // 将圆形数据加载到 SharpGL 预览控件
            _dxfPreviewControl?.LoadCircles(_circles);
            AddLog($"✅ SharpGL 预览控件已加载 {_circles.Count} 个圆形数据");
            
            // 更新状态信息
            UpdateEntityCount(_circles.Count);
            UpdateHolePositions(_circles.Count);
            UpdatePathPoints(_circles.Count);
            
            UpdateProcessingStatus($"解析完成，共找到 {_circles.Count} 个圆形实体");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"解析圆形实体失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            UpdateProcessingStatus("解析失败");
        }
    }

    private void UpdateConfigFromUI()
    {
        // 圆形检测相关参数
        if (float.TryParse(_txtCircleTolerance?.Text, out var circleTolerance))
            _config.CircleDetectionTolerance = circleTolerance;
        
        if (float.TryParse(_txtCenterTolerance?.Text, out var centerTolerance))
            _config.CenterPointTolerance = centerTolerance;
        
        if (float.TryParse(_txtMinRadius?.Text, out var minRadius))
            _config.MinRadius = minRadius;

        // 最大半径
        if (float.TryParse(_txtMaxRadius?.Text, out var maxRadius))
            _config.MaxRadius = maxRadius;

        // 缩放比例
        if (float.TryParse(_txtScale?.Text, out var scale))
            _config.Scale = scale;

        // 移动速度
        if (float.TryParse(_txtMoveVelocity?.Text, out var moveVelocity))
            _config.MoveVelocity = moveVelocity;

        // 加工速度
        if (float.TryParse(_txtProcessVelocity?.Text, out var processVelocity))
            _config.ProcessVelocity = processVelocity;

        // 额外脉冲数
        if (int.TryParse(_txtExtraPulses?.Text, out var extraPulses))
            _config.ExtraPulses = extraPulses;

        // 脉冲周期
        if (float.TryParse(_txtPulsePeriod?.Text, out var pulsePeriod))
            _config.PulsePeriod = pulsePeriod;

        // 路径规划容差1
        if (float.TryParse(_txtPathTolerance1?.Text, out var pathTolerance1))
            _config.PathTolerance1 = pathTolerance1;

        // 路径规划容差2
        if (float.TryParse(_txtPathTolerance2?.Text, out var pathTolerance2))
            _config.PathTolerance2 = pathTolerance2;

        // 加工旋转角度
        if (float.TryParse(_txtRotationAngle?.Text, out float rotationAngle))
        {
            _config.RotationAngle = rotationAngle;
        }

        // 路径算法选择
        if (_cboPathAlgorithm?.SelectedIndex >= 0)
        {
            switch (_cboPathAlgorithm.SelectedIndex)
            {
                case 0:
                    _config.PathAlgorithm = PathGenerationAlgorithm.Cluster;
                    break;
                case 1:
                    _config.PathAlgorithm = PathGenerationAlgorithm.SpiralFill;
                    break;
                case 2:
                    _config.PathAlgorithm = PathGenerationAlgorithm.SnakePath;
                    break;
                case 3:
                    _config.PathAlgorithm = PathGenerationAlgorithm.NearestNeighbor;
                    break;
                case 4:
                    _config.PathAlgorithm = PathGenerationAlgorithm.TestAlgorithm;
                    break;
                case 5:
                    _config.PathAlgorithm = PathGenerationAlgorithm.EnhancedCluster;
                    break;
            }
        }

        // 复选框参数已经在事件处理中直接更新了
        // _config.EnableXYFlip 和 _config.EnableCentering
    }

    private void UpdateEntityCount(int count)
    {
        if (_lblEntityCount != null)
            _lblEntityCount.Text = $"实体数量: {count}";
    }

    private void UpdateHolePositions(int count)
    {
        if (_lblHolePositions != null)
            _lblHolePositions.Text = $"检测孔位: {count}";
    }

    private void UpdatePathPoints(int count)
    {
        if (_lblPathPoints != null)
            _lblPathPoints.Text = $"路径点数: {count}";
    }

    private void UpdateProcessingStatus(string status)
    {
        if (_lblProcessingStatus != null)
            _lblProcessingStatus.Text = $"处理状态: {status}";
    }

    private void OnCirclesStatusUpdated(int total, int circles, int arcs, int polylines)
    {
        // 可以在这里更新更详细的状态信息
    }

    private void OnCircleSelected(CircleEntity circle)
    {
        // 可以在这里处理圆形选择事件
        Console.WriteLine($"选中圆形: {circle.EntityType}, 中心({circle.Center.X:F2}, {circle.Center.Y:F2}), 半径{circle.Radius:F2}");
    }

    private void OnPathCoordinates(object? sender, EventArgs e)
    {
        if (_pathElements.Count == 0)
        {
            MessageBox.Show("请先生成加工路径！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            // 根据路径顺序重新排列圆形实体（使用旋转后的坐标）
            var orderedCircles = new List<CircleEntity>();
            
            // 添加调试信息
            Console.WriteLine($"路径元素数量: {_pathElements.Count}");
            Console.WriteLine($"圆形实体数量: {_circles.Count}");
            Console.WriteLine($"旋转角度: {_config.RotationAngle}度");
            
            int pointIndex = 0;
            foreach (var pathElement in _pathElements)
            {
                if (pathElement.Type == "Point" && pathElement.Data is PointF rotatedPoint)
                {
                    pointIndex++;
                    Console.WriteLine($"路径点{pointIndex}: ({rotatedPoint.X:F4}, {rotatedPoint.Y:F4})");
                    
                    // 找到对应的原始圆形实体
                    CircleEntity? originalCircle = null;
                    float minDistance = float.MaxValue;
                    
                    foreach (var circle in _circles)
                    {
                        // 将原始坐标旋转，与路径点比较
                        var rotatedOriginalPoint = RotatePoint(circle.Center, _config.RotationAngle);
                        float distance = (float)Math.Sqrt(
                            Math.Pow(rotatedOriginalPoint.X - rotatedPoint.X, 2) + 
                            Math.Pow(rotatedOriginalPoint.Y - rotatedPoint.Y, 2)
                        );
                        
                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            originalCircle = circle;
                        }
                    }
                    
                    if (originalCircle != null)
                    {
                        // 使用原始实体的类型和半径，但使用旋转后的坐标
                        var rotatedCircle = new CircleEntity
                        {
                            Index = pointIndex,
                            Center = rotatedPoint, // 使用旋转后的坐标
                            Radius = originalCircle.Radius, // 使用原始半径
                            EntityType = originalCircle.EntityType // 使用原始实体类型
                        };
                        orderedCircles.Add(rotatedCircle);
                        
                        Console.WriteLine($"匹配到原始实体: 类型={originalCircle.EntityType}, 半径={originalCircle.Radius:F4}");
                    }
                    else
                    {
                        Console.WriteLine($"警告: 路径点{pointIndex}未找到匹配的原始实体");
                    }
                }
            }

            Console.WriteLine($"最终排序后的圆形数量: {orderedCircles.Count}");

            // 显示路径坐标窗口
            var pathCoordinatesForm = new DXFtoACSPL.WinForms.Forms.PathCoordinatesForm(orderedCircles);
            pathCoordinatesForm.ShowDialog(this);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"显示路径坐标失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // 工具方法：应用旋转
    private PointF RotatePoint(PointF pt, float angleDeg)
    {
        double angleRad = angleDeg * Math.PI / 180.0;
        float x = (float)(pt.X * Math.Cos(angleRad) - pt.Y * Math.Sin(angleRad));
        float y = (float)(pt.X * Math.Sin(angleRad) + pt.Y * Math.Cos(angleRad));
        return new PointF(x, y);
    }

    private void OnCopyACSPLCode(object? sender, EventArgs e)
    {
        if (_txtACSPLCode != null)
        {
            Clipboard.SetText(_txtACSPLCode.Text);
            MessageBox.Show("ACSPL代码已复制到剪贴板！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void OnSaveACSPLCode(object? sender, EventArgs e)
    {
        if (_txtACSPLCode != null)
        {
            using var saveDialog = new SaveFileDialog
            {
                Filter = "ACSPL代码文件|*.acspl|所有文件|*.*",
                Title = "保存ACSPL代码"
            };

            if (saveDialog.ShowDialog() == DialogResult.OK)
            {
                File.WriteAllText(saveDialog.FileName, _txtACSPLCode.Text);
                MessageBox.Show($"ACSPL代码已保存到 {saveDialog.FileName}", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }

    // 进度条控制方法
    private void ShowProgress(string message)
    {
        if (_progressBar != null)
        {
            _progressBar.Visible = true;
            _progressBar.Value = 0;
            _progressBar.Style = ProgressBarStyle.Marquee;
            UpdateProcessingStatus(message);
            
            // 更新进度条标签
            var progressLabel = _progressBar.Parent?.Controls.OfType<Label>().FirstOrDefault();
            if (progressLabel != null)
            {
                progressLabel.Text = message;
            }
        }
    }

    private void UpdateProgress(int value, int maximum = 100)
    {
        if (_progressBar != null)
        {
            _progressBar.Style = ProgressBarStyle.Continuous;
            _progressBar.Maximum = maximum;
            _progressBar.Value = value;
            
            // 更新进度条标签
            var progressLabel = _progressBar.Parent?.Controls.OfType<Label>().FirstOrDefault();
            if (progressLabel != null)
            {
                progressLabel.Text = $"进度: {value}/{maximum}";
            }
        }
    }

    private void HideProgress()
    {
        if (_progressBar != null)
        {
            _progressBar.Visible = false;
            
            // 重置进度条标签
            var progressLabel = _progressBar.Parent?.Controls.OfType<Label>().FirstOrDefault();
            if (progressLabel != null)
            {
                progressLabel.Text = "就绪";
            }
        }
    }

    // 日志相关方法
    private void AddLog(string message)
    {
        if (_logTextBox != null)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var logEntry = $"[{timestamp}] {message}";
            
            // 在UI线程中更新日志
            if (_logTextBox.InvokeRequired)
            {
                _logTextBox.Invoke(new Action(() => AddLog(message)));
                return;
            }
            
            _logTextBox.AppendText(logEntry + Environment.NewLine);
            _logTextBox.ScrollToCaret();
            
            // 同时输出到控制台
            Console.WriteLine(logEntry);
        }
    }

    private void StartOperationLog(string operationName)
    {
        _operationStartTime = DateTime.Now;
        AddLog($"开始操作: {operationName}");
    }

    private void EndOperationLog(string operationName)
    {
        var duration = DateTime.Now - _operationStartTime;
        AddLog($"完成操作: {operationName} (耗时: {duration.TotalMilliseconds:F0}ms)");
    }

    private void OnClearLog(object? sender, EventArgs e)
    {
        // 清空日志
        AddLog("日志已清空");
    }

    private void OnSaveLog(object? sender, EventArgs e)
    {
        // 保存日志到文件
        using var saveDialog = new SaveFileDialog
        {
            Filter = "日志文件|*.log|文本文件|*.txt|所有文件|*.*",
            Title = "保存处理日志"
        };

        if (saveDialog.ShowDialog() == DialogResult.OK)
        {
            // 这里可以保存日志内容
            File.WriteAllText(saveDialog.FileName, "处理日志内容");
            MessageBox.Show($"日志已保存到 {saveDialog.FileName}", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void OnPreviewLogMessage(string message)
    {
        AddLog($"预览控件日志: {message}");
    }
}