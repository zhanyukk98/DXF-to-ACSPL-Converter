using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DXFtoACSPL.Core.Models;

namespace DXFtoACSPL.WinForms.Forms
{
    public partial class PathCoordinatesForm : Form
    {
        private DataGridView _pathDataGridView;
        private List<CircleEntity> _orderedCircles;

        public PathCoordinatesForm(List<CircleEntity> orderedCircles)
        {
            _orderedCircles = orderedCircles ?? new List<CircleEntity>();
            InitializeComponent();
            InitializeDataGridView();
            LoadData();
        }

        private void InitializeComponent()
        {
            this.Text = "路径坐标列表";
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MinimizeBox = true;
            this.MaximizeBox = true;

            // 创建工具栏
            var toolStrip = new ToolStrip();
            var copyButton = new ToolStripButton("复制到剪贴板", null, OnCopyToClipboard);
            toolStrip.Items.Add(copyButton);

            // 创建数据网格
            _pathDataGridView = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = true,
                BackgroundColor = Color.White,
                GridColor = Color.LightGray,
                BorderStyle = BorderStyle.None,
                Location = new Point(0, 25), // 确保位置正确
                Size = new Size(this.Width, this.Height - 25) // 确保大小正确
            };

            // 布局
            this.Controls.Add(toolStrip);
            this.Controls.Add(_pathDataGridView);
            toolStrip.Dock = DockStyle.Top;
            _pathDataGridView.Dock = DockStyle.Fill;
        }

        private void InitializeDataGridView()
        {
            _pathDataGridView.Columns.Clear();

            // 添加列
            _pathDataGridView.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Index",
                HeaderText = "序号",
                DataPropertyName = "Index",
                Width = 60,
                ReadOnly = true
            });

            _pathDataGridView.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "EntityType",
                HeaderText = "实体类型",
                DataPropertyName = "EntityType",
                Width = 100,
                ReadOnly = true
            });

            _pathDataGridView.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Center",
                HeaderText = "圆心坐标",
                DataPropertyName = "Center",
                Width = 150,
                ReadOnly = true
            });

            _pathDataGridView.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Radius",
                HeaderText = "半径",
                DataPropertyName = "Radius",
                Width = 80,
                ReadOnly = true
            });

            _pathDataGridView.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "BlockName",
                HeaderText = "块名称",
                DataPropertyName = "BlockName",
                Width = 100,
                ReadOnly = true
            });

            _pathDataGridView.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "InsertName",
                HeaderText = "插入名称",
                DataPropertyName = "InsertName",
                Width = 100,
                ReadOnly = true
            });

            _pathDataGridView.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Parameters",
                HeaderText = "参数",
                DataPropertyName = "Parameters",
                Width = 200,
                ReadOnly = true
            });

            // 设置列标题样式
            _pathDataGridView.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(45, 66, 91);
            _pathDataGridView.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            _pathDataGridView.ColumnHeadersDefaultCellStyle.Font = new Font("Microsoft YaHei UI", 9, FontStyle.Bold);
            _pathDataGridView.ColumnHeadersHeight = 35;
            _pathDataGridView.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            // 设置行样式
            _pathDataGridView.RowsDefaultCellStyle.Font = new Font("Microsoft YaHei UI", 9);
            _pathDataGridView.RowsDefaultCellStyle.BackColor = Color.White;
            _pathDataGridView.RowsDefaultCellStyle.SelectionBackColor = Color.FromArgb(0, 122, 204);
            _pathDataGridView.RowsDefaultCellStyle.SelectionForeColor = Color.White;
            _pathDataGridView.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(245, 245, 245);
        }

        private void LoadData()
        {
            _pathDataGridView.Rows.Clear();

            for (int i = 0; i < _orderedCircles.Count; i++)
            {
                var circle = _orderedCircles[i];
                var row = new DataGridViewRow();
                row.CreateCells(_pathDataGridView);

                row.Cells[0].Value = i + 1; // 序号（从1开始）
                row.Cells[1].Value = circle.EntityType;
                row.Cells[2].Value = $"({circle.Center.X:F4}, {circle.Center.Y:F4})";
                row.Cells[3].Value = circle.Radius.ToString("F4");
                row.Cells[4].Value = circle.BlockName;
                row.Cells[5].Value = circle.InsertName;
                row.Cells[6].Value = circle.Parameters;

                _pathDataGridView.Rows.Add(row);
            }

            // 更新标题显示总数
            this.Text = $"路径坐标列表 - 共 {_orderedCircles.Count} 个实体";
        }

        private void OnCopyToClipboard(object sender, EventArgs e)
        {
            try
            {
                var clipboardText = new System.Text.StringBuilder();

                // 添加标题行
                clipboardText.AppendLine("序号\t实体类型\t圆心坐标\t半径\t块名称\t插入名称\t参数");

                // 添加数据行
                for (int i = 0; i < _orderedCircles.Count; i++)
                {
                    var circle = _orderedCircles[i];
                    clipboardText.AppendLine($"{i + 1}\t{circle.EntityType}\t({circle.Center.X:F4}, {circle.Center.Y:F4})\t{circle.Radius:F4}\t{circle.BlockName}\t{circle.InsertName}\t{circle.Parameters}");
                }

                Clipboard.SetText(clipboardText.ToString());
                MessageBox.Show("路径坐标已复制到剪贴板！", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"复制失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
} 