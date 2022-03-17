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
using QPlatform.OPCUAServer;
using QPlatform.Alarm;
using QPlatform.Retentivity;
using QPlatform.Recipe;
using QPlatform.Report;
using QPlatform.EventLogger;
#endregion

/*
 * Logic that allows the removal of a slab strip
 */

public class SlabStripConfigPanelLogic : QPlatform.NetLogic.BaseNetLogic
{
    public override void Start()
    {
        var stripContext = Owner.Get<IUAVariable>("StripContext");
        var slabStripNodeId = (NodeId)stripContext.Value;
        slabStripNode = LogicObject.Context.GetNode(slabStripNodeId);

        xVar = slabStripNode.Get<IUAVariable>("X");
        widthVar = slabStripNode.Get<IUAVariable>("Width");
        heightVar = slabStripNode.Get<IUAVariable>("Height");

        var stripPenNodePointer = Owner.Get<IUAVariable>("StripPen");
        var stripPen = LogicObject.Context.GetNode(stripPenNodePointer.Value);
        if (stripPen == null)
        {
            Log.Error("SlabStripConfigPanelLogic: strip pen not found");
            return;
        }

        xyChartPen = (XYChartPolygonPen)stripPen;

        xVar.VariableChange += UpdateSlabStripPenPoints;
        widthVar.VariableChange += UpdateSlabStripPenPoints;
        heightVar.VariableChange += UpdateSlabStripPenPoints;
    }

    public override void Stop()
    {

    }

    [ExportMethod]
    public void DeleteSlabStrip()
    {
        if (slabStripNode == null || xyChartPen == null)
            return;

        slabStripNode.Delete();
        Owner.Delete();
        xyChartPen.Delete();
    }

    private void UpdateSlabStripPenPoints(object sender, VariableChangeEventArgs e)
    {
        double[,] points = new double[2, 4];

        points[0, 0] = xVar.Value;
        points[1, 0] = 0;

        points[0, 1] = points[0, 0] + widthVar.Value;
        points[1, 1] = 0;

        points[0, 2] = points[0, 1];
        points[1, 2] = heightVar.Value;

        points[0, 3] = points[0, 0];
        points[1, 3] = heightVar.Value;

        xyChartPen.PointArray = points;
    }

    private IUANode slabStripNode;
    private IUAVariable xVar;
    private IUAVariable widthVar;
    private IUAVariable heightVar;
    private XYChartPolygonPen xyChartPen;
}
