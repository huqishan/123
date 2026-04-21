using System;
using System.Windows;

namespace WpfApp.Flowchart
{
    public class FlowchartNodeModel
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Text { get; set; } = string.Empty;
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; } = 150;
        public double Height { get; set; } = 70;

        public Rect GetBounds()
        {
            return new Rect(X, Y, Width, Height);
        }

        public Point GetAnchorPoint(FlowchartAnchor anchor)
        {
            return anchor switch
            {
                FlowchartAnchor.Top => new Point(X + (Width / 2), Y),
                FlowchartAnchor.Right => new Point(X + Width, Y + (Height / 2)),
                FlowchartAnchor.Bottom => new Point(X + (Width / 2), Y + Height),
                _ => new Point(X, Y + (Height / 2))
            };
        }
    }
}
