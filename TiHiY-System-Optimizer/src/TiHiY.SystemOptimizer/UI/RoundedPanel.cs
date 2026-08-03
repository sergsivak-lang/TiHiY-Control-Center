using System.Drawing.Drawing2D;
namespace TiHiY.SystemOptimizer.UI;
internal sealed class RoundedPanel:Panel
{
    public int Radius{get;set;}=18;
    protected override void OnPaint(PaintEventArgs e){ e.Graphics.SmoothingMode=SmoothingMode.AntiAlias; using var path=GetPath(ClientRectangle,Radius); using var brush=new SolidBrush(BackColor); e.Graphics.FillPath(brush,path); }
    protected override void OnResize(EventArgs e){ base.OnResize(e); Region=new Region(GetPath(ClientRectangle,Radius)); }
    private static GraphicsPath GetPath(Rectangle r,int radius){ var d=radius*2; var p=new GraphicsPath(); p.AddArc(r.X,r.Y,d,d,180,90); p.AddArc(r.Right-d,r.Y,d,d,270,90); p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90); p.AddArc(r.X,r.Bottom-d,d,d,90,90); p.CloseFigure(); return p; }
}
