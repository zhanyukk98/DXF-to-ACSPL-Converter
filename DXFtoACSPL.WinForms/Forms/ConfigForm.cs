using DXFtoACSPL.Core.Models;

namespace DXFtoACSPL.WinForms.Forms;

public partial class ConfigForm : Form
{
    private ProcessingConfig _config;

    public ConfigForm(ProcessingConfig config)
    {
        InitializeComponent();
        _config = config;
        InitializeUI();
    }

    private void InitializeUI()
    {
        this.Text = "处理配置";
        this.Size = new Size(400, 300);
        this.StartPosition = FormStartPosition.CenterParent;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;

        CreateControls();
    }

    private void CreateControls()
    {
        var panel = new Panel { Dock = DockStyle.Fill };
        
        var label = new Label
        {
            Text = "配置功能正在开发中...\n\n当前使用默认配置参数",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter
        };

        var button = new Button
        {
            Text = "确定",
            Size = new Size(80, 30),
            Location = new Point((Width - 80) / 2, Height - 50)
        };
        button.Click += (s, e) => this.DialogResult = DialogResult.OK;

        panel.Controls.Add(label);
        panel.Controls.Add(button);
        this.Controls.Add(panel);
    }

    public ProcessingConfig GetConfig()
    {
        return _config;
    }
} 