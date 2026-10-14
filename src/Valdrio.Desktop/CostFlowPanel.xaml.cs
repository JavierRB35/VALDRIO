using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
namespace Valdrio.Desktop;
public partial class CostFlowPanel : UserControl
{
    private CostFlowViewModel? vm;
    public CostFlowPanel()
    {
        InitializeComponent(); DataContextChanged += (_, _) =>
        { if(vm is not null) vm.GraphChanged -= Changed; vm=DataContext as CostFlowViewModel; if(vm is not null) vm.GraphChanged += Changed; Draw(); };
    }
    private void PanGraph(object sender, RoutedPropertyChangedEventArgs<double> e) => GraphViewport?.ScrollToHorizontalOffset(e.NewValue);
    private void Changed(object? sender, EventArgs e) => Draw();
    private void Draw()
    {
        GraphCanvas.Children.Clear(); if(vm is null) return;
        var headers=new[]{"Stage","Resource","Source","Destination","Rule","Assigned","Received","Terminal"};
        for(var i=0;i<headers.Length;i++) DetailsGrid.Columns[i].Header=vm[headers[i]];
        headers=["Resource","Effective","Direct","Excluded","Pending","Terminal","Difference"];
        for(var i=0;i<headers.Length;i++) ReconciliationGrid.Columns[i].Header=vm[headers[i]];
        var max=vm.Nodes.GroupBy(n=>n.Stage).Select(g=>g.Count()).DefaultIfEmpty(1).Max();
        GraphCanvas.Width=Math.Max(1100,max*310+60);
        double[] ys=[65,365,695,1170];
        var positions=new Dictionary<string,Point>();
        for(var stage=0;stage<4;stage++)
        {
            var title=new TextBlock{Text=vm.Stages[stage],FontSize=18,FontWeight=FontWeights.SemiBold,Foreground=(Brush)FindResource("Brand.DeepGreen")};
            Canvas.SetLeft(title,24);Canvas.SetTop(title,ys[stage]-45);GraphCanvas.Children.Add(title);
            var nodes=vm.Nodes.Where(n=>n.Stage==stage).OrderBy(n=>n.Label,StringComparer.CurrentCulture).ToArray();
            for(var i=0;i<nodes.Length;i++) positions[nodes[i].Id]=new((GraphCanvas.Width-nodes.Length*310)/2+i*310,ys[stage]);
        }
        int internalIndex=0; int edgeIndex=0; int terminalIndex=0;
        foreach(var edge in vm.Arrows)
        {
            if(!positions.TryGetValue(edge.SourceId,out var from)||!positions.TryGetValue(edge.DestinationId,out var to)) continue;
            var start=new Point(from.X+135,from.Y+110); var end=new Point(to.X+135,to.Y);
            Point c1,c2,label;
            if(edge.Internal)
            {
                var lane=870+80*(internalIndex++%3);end=new(to.X+145,to.Y+110);
                c1=new(start.X,lane);c2=new(end.X,lane);label=new((start.X+end.X)/2-110,lane-12);
            }
            else
            {
                var lane=(start.Y+end.Y)/2 + 30*(edgeIndex++%3-1);
                if(from.Y==ys[2] && to.Y==ys[3]) lane=1040+50*(terminalIndex++%2);
                c1=new(start.X,lane);c2=new(end.X,lane);label=new((start.X+end.X)/2-105,lane-12);
                if(from.Y<500 && to.Y>1000) { c1=new(GraphCanvas.Width-15,start.Y+80);c2=new(GraphCanvas.Width-15,end.Y-80);label=new(GraphCanvas.Width-230,1050); }
            }
            var geometry=new PathGeometry([new PathFigure(start,edge.Internal ? [new PolyLineSegment([c1,c2,end],true)] : [new BezierSegment(c1,c2,end,true)],false)]);
            var path=new Path{Data=geometry,Stroke=(Brush)FindResource(edge.Internal?"Accent.ActiveGreen":"Brand.DeepGreen"),StrokeThickness=2};
            if(edge.Internal)path.StrokeDashArray=[4,2]; GraphCanvas.Children.Add(path);
            var arrow=new Polygon{Points=new PointCollection(edge.Internal?[new(end.X-5,end.Y+8),end,new(end.X+5,end.Y+8)]:[new(end.X-5,end.Y-8),end,new(end.X+5,end.Y-8)]),Fill=path.Stroke};GraphCanvas.Children.Add(arrow);
            var button=new Button{Content=new TextBlock{Text=edge.Label,TextWrapping=TextWrapping.Wrap,TextAlignment=TextAlignment.Center},Width=210,ToolTip=edge.Row?.Detail??edge.Label,Padding=new Thickness(4),MinHeight=30};
            button.Click+=(_,_)=>{vm.SelectArrow(edge);};
            System.Windows.Automation.AutomationProperties.SetName(button,edge.Label);
            Canvas.SetLeft(button,label.X);Canvas.SetTop(button,label.Y);GraphCanvas.Children.Add(button);
        }
        foreach(var node in vm.Nodes)
        {
            var point=positions[node.Id];var stack=new StackPanel();
            stack.Children.Add(new TextBlock{Text=node.Label,TextWrapping=TextWrapping.Wrap,TextAlignment=TextAlignment.Center});
            stack.Children.Add(new TextBlock{Text=node.Amount,TextWrapping=TextWrapping.Wrap,TextAlignment=TextAlignment.Center,FontWeight=FontWeights.Bold,Margin=new Thickness(0,6,0,0)});
            var button=new Button{Content=stack,Width=270,Height=110,Padding=new Thickness(10),Background=(Brush)FindResource(node.Stage==3?"Status.SuccessBg":"Surface.Subtle"),ToolTip=node.Label};
            button.Click+=(_,_)=>vm.SelectNode(node.Id);System.Windows.Automation.AutomationProperties.SetName(button,node.Label+" "+node.Amount);
            Canvas.SetLeft(button,point.X);Canvas.SetTop(button,point.Y);GraphCanvas.Children.Add(button);
        }
    }
}
