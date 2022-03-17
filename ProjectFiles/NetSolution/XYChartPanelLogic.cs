#region StandardUsing
using System;
using QPlatform.Core;
using QPlatform.CoreBase;
using QPlatform.HMIProject;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using QPlatform.NetLogic;
using QPlatform.UI;
using QPlatform.Store;
using QPlatform.SQLiteStore;
using QPlatform.Alarm;
using QPlatform.OPCUAServer;
using QPlatform.Recipe;
using QPlatform.Retentivity;
using System.Globalization;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using QPlatform.Report;
using QPlatform.EventLogger;
#endregion

/*
 * Logic used by the XYChartPanel page for:
 *  - Laser operation via a periodic task
 *  - Adding a new slab strip
 *  - Modifying and redrawing the XY chart 
 */

public class XYChartPanelLogic : QPlatform.NetLogic.BaseNetLogic
{
    public override void Start()
    {
        slabStripsNode = Project.Current.Children["Model"].GetObject("SlabStrips");
        if (slabStripsNode == null)
        {
            Log.Error("XYChartPanelLogic: Model/SlabStrips object not found");
            return;
        }

        var slabStripWidgetsContainerNodePointer = LogicObject.Get<IUAVariable>("SlabStripWidgetsContainer");
        var slabStripWidgetsContainerNode = LogicObject.Context.GetNode(slabStripWidgetsContainerNodePointer.Value);
        if (slabStripWidgetsContainerNode == null)
        {
            Log.Error("XYChartPanelLogic: slab strip widgets container not found");
            return;
        }

        slabStripWidgetsContainer = (Item)slabStripWidgetsContainerNode;

        xyChart = (XYChart)Owner.GetObject("XyChart");
        slabPoints = LoadSlabPolygon();

        InitializeSlabStrips();

        if (slabPoints != null)
        {
            laserTask = new PeriodicTask(LaserTask, 200, LogicObject);
            laserTask.Start();
        }
    }

    public override void Stop()
    {
        if (laserTask != null)
        {
            laserTask.Cancel();
            laserTask.Dispose();
        }
    }

    [ExportMethod]
    public void AddNewSlabStrip()
    {
        if (slabStripsNode.Children.Count >= maxStripCount)
            return;

        SlabStrip tmpStrip = null;
        foreach (var slab in slabStripsNode.Children.OfType<SlabStrip>())
        {
            if (tmpStrip == null || slab.X > tmpStrip.X)
                tmpStrip = slab;
        }

        // Create SlabStrip node
        var stripId = slabStripsNode.Children.Count + 1;
        var newSlabStripNode = InformationModel.MakeObject<SlabStrip>("SlabStrip" + stripId);
        slabStripsNode.Add(newSlabStripNode);

        newSlabStripNode.X = tmpStrip != null ? tmpStrip.X + tmpStrip.Width : 0;

        // Create SlabStrip XYChartPolygonPen
        var stripPen = CreateSlabStripPen(newSlabStripNode);
        xyChart.Pens.Add(stripPen);

        // Add new StripConfigWidget
        AddStripConfigWidget(newSlabStripNode, slabStripsNode.Children.Count, stripPen);
    }


    [ExportMethod]
    public void UpdateSlabStrip(NodeId penNodeId, NodeId slabStripNodeId)
    {
        var penNode = LogicObject.Context.GetObject(penNodeId);
        if (penNode == null)
            return;

        var xyChartPen = (XYChartPolygonPen)penNode;

        var slabStripNode = LogicObject.Context.GetObject(slabStripNodeId);
        if (slabStripNode == null)
            return;

        var slabStrip = (SlabStrip)slabStripNode;

        UpdateSlabStripPenPoints(xyChartPen, slabStrip);
    }

    private XYChartScalarLinePen AddLaserPen()
    {
        var laserPen = InformationModel.MakeObject<XYChartScalarLinePen>("LaserPen");
        laserPen.Thickness = 3;
        laserPen.Color = laserColor;

        try
        {
            xyChart.Pens.Add(laserPen);
        }
        catch
        {
            laserPen.Delete();
            laserPen = null;
        }

        return laserPen;
    }

    private void RemoveLaserPen()
    {
        xyChart.Pens.Remove("LaserPen");
    }

    private void LaserTask()
    {
        if (currentLaserPoint >= slabPoints.GetLength(1))
        {
            currentLaserPoint = 0;
            RemoveLaserPen();
        }

        if (currentLaserPoint == 0)
        {
            Thread.Sleep(1000);
            laserPen = AddLaserPen();
        }

        if (laserPen == null)
            return;

        laserPen.X = slabPoints[0, currentLaserPoint];
        laserPen.Y = slabPoints[1, currentLaserPoint];

        ++currentLaserPoint;
    }

    private void InitializeSlabStrips()
    {
        int i = 0;
        foreach (var slabStrip in slabStripsNode.Children.OfType<SlabStrip>())
        {
            var slabStripPen = CreateSlabStripPen(slabStrip);
            ++i;

            xyChart.Pens.Add(slabStripPen);
            AddStripConfigWidget(slabStrip, i + 1, slabStripPen);
        }
    }

    private XYChartPolygonPen CreateSlabStripPen(SlabStrip slabStrip)
    {
        var slabStripPen = InformationModel.MakeObject<XYChartPolygonPen>(slabStrip.BrowseName);
        slabStripPen.PointArrayVariable.ArrayDimensions = new uint[] { 2, 0 };

        UpdateSlabStripPenFillColor(slabStripPen);
        slabStripPen.BorderColor = slabStripPen.FillColor;
        UpdateSlabStripPenPoints(slabStripPen, slabStrip);

        return slabStripPen;
    }

    private void UpdateSlabStripPenFillColor(XYChartPolygonPen slabStripPen)
    {
        slabStripPen.FillColor = xyChart.Pens.Count % 2 == 0 ? evenColor : oddColor;
    }

    private void UpdateSlabStripPenPoints(XYChartPolygonPen slabStripPen, SlabStrip slabStrip)
    {
        double[,] points = new double[2, 4];

        var xVar = slabStrip.Get<IUAVariable>("X");
        var widthVar = slabStrip.Get<IUAVariable>("Width");
        var heightVar = slabStrip.Get<IUAVariable>("Height");

        points[0, 0] = xVar.Value;
        points[1, 0] = 0;

        points[0, 1] = points[0, 0] + widthVar.Value;
        points[1, 1] = 0;

        points[0, 2] = points[0, 1];
        points[1, 2] = heightVar.Value;

        points[0, 3] = points[0, 0];
        points[1, 3] = heightVar.Value;

        slabStripPen.PointArray = points;
    }

    private double[,] LoadSlabPolygon()
    {
        var slabFileName = Path.Combine(Project.Current.ProjectDirectory, "slab.csv");
        if (!File.Exists(slabFileName))
        {
            Log.Error("Slab.csv not found");
            return null;
        }

        var slabPen = InformationModel.MakeObject<XYChartPolygonPen>("Slab");
        slabPen.PointArrayVariable.ArrayDimensions = new uint[] { 2, 0 };
        slabPen.FillColor = new Color(255, 120, 120, 120);
        slabPen.BorderColor = slabPen.FillColor;

        var cultureInfo = CultureInfo.CreateSpecificCulture("en-US");
        var lines = File.ReadAllLines(slabFileName);

        var points = new double[2, lines.Length];
        int counter = 0;
        foreach (var l in lines)
        {
            var tokens = l.Split(',');

            points[0, counter] = Double.Parse(tokens[0], cultureInfo);
            points[1, counter] = Double.Parse(tokens[1], cultureInfo);

            counter++;
        }

        slabPen.PointArray = points;

        xyChart.Pens.Add(slabPen);

        return points;
    }

    private void AddStripConfigWidget(SlabStrip slabStrip, int stripId, XYChartPolygonPen stripPen)
    {
        var slabStripConfigWidget = InformationModel.MakeObject<IUAObject>("SlabStripConfigPanel",
                                                                           BretonDemo.ObjectTypes.SlabStripConfigPanel);

        var stripContext = slabStripConfigWidget.Get<IUAVariable>("StripContext");
        stripContext.Value = slabStrip.NodeId;

        var stripPenNodePointer = slabStripConfigWidget.Get<IUAVariable>("StripPen");
        stripPenNodePointer.Value = stripPen.NodeId;

        var stripIdVariable = slabStripConfigWidget.Get<IUAVariable>("StripId");
        stripIdVariable.Value = stripId;

        slabStripWidgetsContainer.Add(slabStripConfigWidget);
    }


    private IUAObject slabStripsNode;
    private PeriodicTask laserTask;
    private XYChart xyChart;
    private XYChartScalarLinePen laserPen;
    private double[,] slabPoints;
    private int currentLaserPoint = 0;
    private int maxStripCount = 8;
    private Item slabStripWidgetsContainer;
    private Color evenColor = new Color(150, 127, 201, 74);
    private Color oddColor = new Color(80, 127, 201, 74);
    private Color laserColor = new Color(255, 240, 45, 45);
}
