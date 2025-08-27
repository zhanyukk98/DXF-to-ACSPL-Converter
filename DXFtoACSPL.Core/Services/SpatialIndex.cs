 using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace DXFtoACSPL.Core.Services
{
    /// <summary>
    /// 网格单元
    /// </summary>
    public class GridCell
    {
        public bool IsRegular { get; set; } = true;
        public List<PointF> Points { get; set; } = new List<PointF>();
        public GridCell[] Subdivision { get; set; } = null;
        public PointF[] ConvexHull { get; set; } = null;
        public float Density { get; set; } = 0.0f;
        public int GridX { get; set; }
        public int GridY { get; set; }
    }

    /// <summary>
    /// 混合索引结构：粗略网格 + 密度聚类
    /// </summary>
    public class HybridIndex
    {
        private readonly List<PointF> _points;
        private readonly GridCell[,] _coarseGrid;
        private readonly int _gridWidth;
        private readonly int _gridHeight;
        private readonly float _cellWidth;
        private readonly float _cellHeight;
        private readonly RectangleF _bounds;
        private readonly float _avgDistance;
        
        public HybridIndex(List<PointF> points, int gridSize = 50)
        {
            _points = points;
            _bounds = CalculateBounds(points);
            _avgDistance = CalculateAverageNearestNeighborDistance(points);
            
            _gridWidth = gridSize;
            _gridHeight = gridSize;
            _cellWidth = _bounds.Width / _gridWidth;
            _cellHeight = _bounds.Height / _gridHeight;
            
            _coarseGrid = new GridCell[_gridWidth, _gridHeight];
            
            BuildCoarseGrid();
            AnalyzeDensityDistribution();
            OptimizeGridStructure();
        }

        private RectangleF CalculateBounds(List<PointF> points)
        {
            if (points.Count == 0) return new RectangleF(0, 0, 1, 1);
            
            float minX = points.Min(p => p.X);
            float maxX = points.Max(p => p.X);
            float minY = points.Min(p => p.Y);
            float maxY = points.Max(p => p.Y);
            
            return new RectangleF(minX, minY, maxX - minX, maxY - minY);
        }

        private float CalculateAverageNearestNeighborDistance(List<PointF> points)
        {
            if (points.Count < 2) return 1.0f;
            
            float totalDistance = 0;
            int sampleSize = Math.Min(1000, points.Count);
            
            for (int i = 0; i < sampleSize; i++)
            {
                var point = points[i];
                float minDist = float.MaxValue;
                
                for (int j = 0; j < Math.Min(50, points.Count); j++)
                {
                    if (i == j) continue;
                    var dist = EuclideanDistance(point, points[j]);
                    if (dist < minDist) minDist = dist;
                }
                
                totalDistance += minDist;
            }
            
            return totalDistance / sampleSize;
        }

        private void BuildCoarseGrid()
        {
            for (int x = 0; x < _gridWidth; x++)
            {
                for (int y = 0; y < _gridHeight; y++)
                {
                    _coarseGrid[x, y] = new GridCell { GridX = x, GridY = y };
                }
            }
            
            foreach (var point in _points)
            {
                var (gridX, gridY) = PointToGridCoords(point);
                if (gridX >= 0 && gridX < _gridWidth && gridY >= 0 && gridY < _gridHeight)
                {
                    _coarseGrid[gridX, gridY].Points.Add(point);
                }
            }
        }

        private void AnalyzeDensityDistribution()
        {
            float totalCells = _gridWidth * _gridHeight;
            float avgPointsPerCell = _points.Count / totalCells;
            
            for (int x = 0; x < _gridWidth; x++)
            {
                for (int y = 0; y < _gridHeight; y++)
                {
                    var cell = _coarseGrid[x, y];
                    cell.Density = cell.Points.Count / avgPointsPerCell;
                    
                    if (cell.Points.Count > avgPointsPerCell * 3)
                    {
                        cell.IsRegular = false;
                    }
                    else if (cell.Points.Count == 0)
                    {
                        cell.IsRegular = false;
                    }
                }
            }
        }

        private void OptimizeGridStructure()
        {
            for (int x = 0; x < _gridWidth; x++)
            {
                for (int y = 0; y < _gridHeight; y++)
                {
                    var cell = _coarseGrid[x, y];
                    
                    if (cell.Points.Count > 100)
                    {
                        SubdivideCell(cell, x, y);
                    }
                }
            }
        }

        private void SubdivideCell(GridCell cell, int gridX, int gridY)
        {
            cell.Subdivision = new GridCell[4];
            
            var cellBounds = GetCellBounds(gridX, gridY);
            float halfWidth = cellBounds.Width / 2;
            float halfHeight = cellBounds.Height / 2;
            
            for (int i = 0; i < 4; i++)
            {
                cell.Subdivision[i] = new GridCell();
            }
            
            foreach (var point in cell.Points)
            {
                int subIndex = 0;
                if (point.X > cellBounds.X + halfWidth) subIndex += 1;
                if (point.Y > cellBounds.Y + halfHeight) subIndex += 2;
                
                cell.Subdivision[subIndex].Points.Add(point);
            }
        }

        private RectangleF GetCellBounds(int gridX, int gridY)
        {
            float x = _bounds.X + gridX * _cellWidth;
            float y = _bounds.Y + gridY * _cellHeight;
            return new RectangleF(x, y, _cellWidth, _cellHeight);
        }

        private (int gridX, int gridY) PointToGridCoords(PointF point)
        {
            int gridX = (int)((point.X - _bounds.X) / _cellWidth);
            int gridY = (int)((point.Y - _bounds.Y) / _cellHeight);
            
            gridX = Math.Max(0, Math.Min(_gridWidth - 1, gridX));
            gridY = Math.Max(0, Math.Min(_gridHeight - 1, gridY));
            
            return (gridX, gridY);
        }

        public List<PointF> FindNearestPoints(PointF target, int k, HashSet<PointF> excludeSet = null)
        {
            var (gridX, gridY) = PointToGridCoords(target);
            var candidates = new List<PointF>();
            
            int radius = 0;
            while (candidates.Count < k * 3 && radius < Math.Max(_gridWidth, _gridHeight))
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        if (Math.Abs(dx) != radius && Math.Abs(dy) != radius) continue;
                        
                        int nx = gridX + dx;
                        int ny = gridY + dy;
                        
                        if (nx >= 0 && nx < _gridWidth && ny >= 0 && ny < _gridHeight)
                        {
                            var cell = _coarseGrid[nx, ny];
                            AddCellPoints(cell, candidates, excludeSet);
                        }
                    }
                }
                radius++;
            }
            
            return candidates
                .Select(p => new { Point = p, Distance = EuclideanDistance(target, p) })
                .OrderBy(x => x.Distance)
                .Take(k)
                .Select(x => x.Point)
                .ToList();
        }

        private void AddCellPoints(GridCell cell, List<PointF> candidates, HashSet<PointF> excludeSet)
        {
            if (cell.Subdivision != null)
            {
                foreach (var subCell in cell.Subdivision)
                {
                    AddCellPoints(subCell, candidates, excludeSet);
                }
            }
            else
            {
                foreach (var point in cell.Points)
                {
                    if (excludeSet?.Contains(point) != true)
                    {
                        candidates.Add(point);
                    }
                }
            }
        }

        public (int totalCells, int nonEmptyCells, int subdivisions, float avgDensity) GetStatistics()
        {
            int totalCells = _gridWidth * _gridHeight;
            int nonEmptyCells = 0;
            int subdivisions = 0;
            float totalDensity = 0;
            
            for (int x = 0; x < _gridWidth; x++)
            {
                for (int y = 0; y < _gridHeight; y++)
                {
                    var cell = _coarseGrid[x, y];
                    if (cell.Points.Count > 0) nonEmptyCells++;
                    if (cell.Subdivision != null) subdivisions++;
                    totalDensity += cell.Density;
                }
            }
            
            return (totalCells, nonEmptyCells, subdivisions, totalDensity / totalCells);
        }

        private float EuclideanDistance(PointF p1, PointF p2)
        {
            float dx = p1.X - p2.X;
            float dy = p1.Y - p2.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
    }
}