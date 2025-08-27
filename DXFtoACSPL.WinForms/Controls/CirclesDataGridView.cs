using DXFtoACSPL.Core.Models;
using System.Drawing;
using System.Windows.Forms;

namespace DXFtoACSPL.WinForms.Controls;

/// <summary>
/// 圆形列表控件
/// </summary>
public class CirclesDataGridView : DataGridView
{
    private List<CircleEntity> _circles = new();

    public CirclesDataGridView()
    {
        InitializeGrid();
    }

    private void InitializeGrid()
    {
        // 设置基本属性
        this.AllowUserToAddRows = false;
        this.AllowUserToDeleteRows = false;
        this.ReadOnly = true;
        this.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        this.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        this.MultiSelect = false;
        this.RowHeadersVisible = true;
        this.EnableHeadersVisualStyles = false;
        this.ColumnHeadersDefaultCellStyle.BackColor = Color.LightBlue;
        this.ColumnHeadersDefaultCellStyle.Font = new Font("Arial", 9, FontStyle.Bold);

        // 创建列
        CreateColumns();

        // 绑定事件
        this.CellFormatting += OnCellFormatting!;
        this.CellClick += OnCellClick!;
    }

    private void CreateColumns()
    {
        this.Columns.Clear();

        // 索引列
        var indexColumn = new DataGridViewTextBoxColumn
        {
            Name = "Index",
            HeaderText = "序号",
            DataPropertyName = "Index",
            Width = 60,
            MinimumWidth = 60
        };

        // 实体类型列
        var typeColumn = new DataGridViewTextBoxColumn
        {
            Name = "EntityType",
            HeaderText = "实体类型",
            DataPropertyName = "EntityType",
            Width = 120,
            MinimumWidth = 100
        };

        // 圆心坐标列
        var centerColumn = new DataGridViewTextBoxColumn
        {
            Name = "Center",
            HeaderText = "圆心坐标",
            DataPropertyName = "Center",
            Width = 120,
            MinimumWidth = 100
        };

        // 半径列
        var radiusColumn = new DataGridViewTextBoxColumn
        {
            Name = "Radius",
            HeaderText = "半径",
            DataPropertyName = "Radius",
            Width = 80,
            MinimumWidth = 80
        };

        // 参数列
        var parametersColumn = new DataGridViewTextBoxColumn
        {
            Name = "Parameters",
            HeaderText = "参数信息",
            DataPropertyName = "Parameters",
            Width = 200,
            MinimumWidth = 150
        };

        // 块名称列
        var blockNameColumn = new DataGridViewTextBoxColumn
        {
            Name = "BlockName",
            HeaderText = "块名称",
            DataPropertyName = "BlockName",
            Width = 100,
            MinimumWidth = 80
        };

        // 插入名称列
        var insertNameColumn = new DataGridViewTextBoxColumn
        {
            Name = "InsertName",
            HeaderText = "插入名称",
            DataPropertyName = "InsertName",
            Width = 100,
            MinimumWidth = 80
        };

        // 添加列
        this.Columns.AddRange(new DataGridViewColumn[]
        {
            indexColumn,
            typeColumn,
            centerColumn,
            radiusColumn,
            parametersColumn,
            blockNameColumn,
            insertNameColumn
        });
    }

    /// <summary>
    /// 设置圆形数据
    /// </summary>
    /// <param name="circles">圆形实体列表</param>
    public void SetCircles(List<CircleEntity> circles)
    {
        _circles = circles ?? new List<CircleEntity>();
        RefreshData();
    }

    /// <summary>
    /// 刷新数据
    /// </summary>
    private void RefreshData()
    {
        this.Rows.Clear();

        // 确保列已创建
        if (this.Columns.Count == 0)
        {
            CreateColumns();
        }

        foreach (var circle in _circles)
        {
            var row = new DataGridViewRow();
            row.CreateCells(this);

            // 使用列索引而不是列名来设置值
            if (this.Columns.Count > 0) row.Cells[0].Value = circle.Index; // Index
            if (this.Columns.Count > 1) row.Cells[1].Value = circle.EntityType; // EntityType
            if (this.Columns.Count > 2) row.Cells[2].Value = $"({circle.Center.X:F4}, {circle.Center.Y:F4})"; // Center
            if (this.Columns.Count > 3) row.Cells[3].Value = circle.Radius.ToString("F4"); // Radius
            if (this.Columns.Count > 4) row.Cells[4].Value = circle.Parameters; // Parameters
            if (this.Columns.Count > 5) row.Cells[5].Value = circle.BlockName ?? ""; // BlockName
            if (this.Columns.Count > 6) row.Cells[6].Value = circle.InsertName ?? ""; // InsertName

            // 根据实体类型设置行颜色
            row.DefaultCellStyle.BackColor = GetRowColor(circle);

            this.Rows.Add(row);
        }

        // 更新状态
        UpdateStatus();
    }

    /// <summary>
    /// 获取行颜色
    /// </summary>
    private Color GetRowColor(CircleEntity circle)
    {
        return circle.EntityType switch
        {
            "圆" => Color.LightBlue,
            "圆弧" => Color.LightGreen,
            "多段线（拟合成圆）" => Color.LightYellow,
            _ => Color.White
        };
    }

    /// <summary>
    /// 更新状态信息
    /// </summary>
    private void UpdateStatus()
    {
        var totalCount = _circles.Count;
        var circleCount = _circles.Count(c => c.EntityType == "圆");
        var arcCount = _circles.Count(c => c.EntityType == "圆弧");
        var polylineCount = _circles.Count(c => c.EntityType == "多段线（拟合成圆）");

        // 这里可以触发事件或更新状态栏
        OnStatusUpdated?.Invoke(totalCount, circleCount, arcCount, polylineCount);
    }

    /// <summary>
    /// 单元格格式化事件
    /// </summary>
    private void OnCellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.Value != null)
        {
            // 格式化数值显示
            if (e.ColumnIndex == this.Columns["Radius"].Index)
            {
                if (float.TryParse(e.Value.ToString(), out float value))
                {
                    e.Value = value.ToString("F4");
                    e.FormattingApplied = true;
                }
            }
        }
    }

    /// <summary>
    /// 单元格点击事件
    /// </summary>
    private void OnCellClick(object sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex >= 0 && e.RowIndex < _circles.Count)
        {
            var circle = _circles[e.RowIndex];
            OnCircleSelected?.Invoke(circle);
        }
    }

    /// <summary>
    /// 获取选中的圆形
    /// </summary>
    public CircleEntity? GetSelectedCircle()
    {
        if (this.CurrentRow != null && this.CurrentRow.Index >= 0 && this.CurrentRow.Index < _circles.Count)
        {
            return _circles[this.CurrentRow.Index];
        }
        return null;
    }

    /// <summary>
    /// 清除数据
    /// </summary>
    public void ClearData()
    {
        _circles.Clear();
        this.Rows.Clear();
        UpdateStatus();
    }

    /// <summary>
    /// 导出数据到CSV
    /// </summary>
    public void ExportToCsv(string filePath)
    {
        try
        {
            using var writer = new StreamWriter(filePath, false, System.Text.Encoding.UTF8);
            
            // 写入表头
            var headers = new string[]
            {
                "序号", "实体类型", "圆心坐标", "半径", "参数信息", "块名称", "插入名称"
            };
            writer.WriteLine(string.Join(",", headers));

            // 写入数据
            foreach (var circle in _circles)
            {
                var values = new string[]
                {
                    circle.Index.ToString(),
                    circle.EntityType,
                    $"({circle.Center.X.ToString("F4")}, {circle.Center.Y.ToString("F4")})",
                    circle.Radius.ToString("F4"),
                    circle.Parameters,
                    circle.BlockName ?? "",
                    circle.InsertName ?? ""
                };
                writer.WriteLine(string.Join(",", values));
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导出CSV失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// 状态更新事件
    /// </summary>
    public event Action<int, int, int, int>? OnStatusUpdated;

    /// <summary>
    /// 圆形选中事件
    /// </summary>
    public event Action<CircleEntity>? OnCircleSelected;
} 