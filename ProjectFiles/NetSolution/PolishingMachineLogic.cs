#region StandardUsing
using System;
using QPlatform.Core;
using System.Linq;
using QPlatform.CoreBase;
using QPlatform.HMIProject;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using QPlatform.UI;
using QPlatform.Report;
using QPlatform.EventLogger;
#endregion

public class PolishingMachineLogic : QPlatform.NetLogic.BaseNetLogic
{
    public override void Start()
    {
        polishingMachine = (PolishingMachine)Owner;
        var countVariable = polishingMachine.Spindles.GetVariable("Count");

        plcLogicObject = Project.Current.GetObject("Model/PLCLogic");

        var numberOfSpindles = GetActualNumberOfSpindles();
        if (numberOfSpindles == 0)
            InitializeSpindles(countVariable.Value);
        countVariable.VariableChange += CountVariable_VariableChange;
    }

    private void CountVariable_VariableChange(object sender, VariableChangeEventArgs e)
    {
        var retainedAlarmsNode = LogicObject.Context.GetObject(QPlatform.Alarm.Objects.RetainedAlarms);
        retainedAlarmsNode.Stop();

        plcLogicObject.ExecuteMethod("StopPLC");
        InitializeSpindles(e.NewValue);
        plcLogicObject.ExecuteMethod("StartPLC");

        retainedAlarmsNode.Start();
    }

    void InitializeSpindles(int count)
    {
        var mainNavigationPanelNodePointer = polishingMachine.Spindles.GetVariable("MainNavigationPanel");
        var mainNavigationPanel = (NavigationPanel)Owner.Context.GetObject(mainNavigationPanelNodePointer.Value);

        if (mainNavigationPanel != null)
            mainNavigationPanel.Enabled = false;

        var spindlesContainer = polishingMachine.Spindles.GetObject("SpindlesContainer");

        foreach (var child in spindlesContainer.Children.OfType<Spindle>())
            child.Delete();

		for (var i = 0; i < count; ++i)
        {
            var spindle = InformationModel.MakeObject<Spindle>("Spindle" + i);
            spindle.ID = i + 1;
            spindle.AbrasiveLevelAlarm.Severity = 200;
            spindle.InputCurrentIntensityAlarm.Severity = 500;

            spindlesContainer.Add(spindle);
		}

        var spindlesFrontUIContainerNodePointer = polishingMachine.Spindles.GetVariable("SpindlesFrontUIContainer");
        var spindlesFrontUIContainer = (Item)Owner.Context.GetObject(spindlesFrontUIContainerNodePointer.Value);
        if (spindlesFrontUIContainer != null)
            InitializeSpindlesFrontContainer(spindlesContainer, spindlesFrontUIContainer);

        var spindlesTopUIContainerNodePointer = polishingMachine.Spindles.GetVariable("SpindlesTopUIContainer");
        var spindlesTopUIContainer = (Item)Owner.Context.GetObject(spindlesTopUIContainerNodePointer.Value);
        if (spindlesTopUIContainer != null)
            InitializeSpindlesTopContainer(spindlesContainer, spindlesTopUIContainer);

        var spindlesInputUIContainerNodePointer = polishingMachine.Spindles.GetVariable("SpindlesInputUIContainer");
        var spindlesInputUIContainer = (Item)Owner.Context.GetObject(spindlesInputUIContainerNodePointer.Value);
        if (spindlesInputUIContainer != null)
            InitializeSpindlesInputContainer(spindlesContainer, spindlesInputUIContainer);

        var spindlesPensNodePointer = polishingMachine.Spindles.GetVariable("SpindlesPensContainer");
        var spindlesPens = Owner.Context.GetObject(spindlesPensNodePointer.Value);
        var spindlesPensCheckboxContainerNodePointer = polishingMachine.Spindles.GetVariable("SpindlesPensCheckboxContainer");
        var spindlesPensCheckboxContainer = Owner.Context.GetObject(spindlesPensCheckboxContainerNodePointer.Value);
        if (spindlesPens != null)
            InitializeSpindlesPens(spindlesPens, spindlesPensCheckboxContainer);

        if (mainNavigationPanel != null)
            mainNavigationPanel.Enabled = true;
    }

    void RemoveUIItemChildren(Item container)
    {
        foreach (var child in container.Children.OfType<Item>())
			child.Delete();
    }

    void InitializeSpindlesFrontContainer(IUAObject spindlesContainer, Item uiContainer)
    {
        RemoveUIItemChildren(uiContainer);

        int i = 0;

        foreach (var spindleNode in spindlesContainer.Children.OfType<Spindle>())
        {
            var item = InformationModel.MakeObject("SpindleFront" + (i++), BretonDemo.ObjectTypes.SpindleFront);
            item.GetVariable("Spindle").Value = spindleNode.NodeId;
            item.ModellingRule = NamingRuleType.Mandatory;
            uiContainer.Add(item);
		}
    }

    void InitializeSpindlesTopContainer(IUAObject spindlesContainer, Item uiContainer)
    {
        RemoveUIItemChildren(uiContainer);

        int i = 0;

        foreach (var spindleNode in spindlesContainer.Children.OfType<Spindle>())
        {
            var item = InformationModel.MakeObject("SpindleTop" + (i++), BretonDemo.ObjectTypes.SpindleTop);
            item.GetVariable("Spindle").Value = spindleNode.NodeId;
            item.ModellingRule = NamingRuleType.Mandatory;
            uiContainer.Add(item);
		}
    }

    void InitializeSpindlesInputContainer(IUAObject spindlesContainer, Item uiContainer)
    {
        RemoveUIItemChildren(uiContainer);

        int i = 0;

        foreach (var spindleNode in spindlesContainer.Children.OfType<Spindle>())
        {
            var item = InformationModel.MakeObject("SpindleInput" + (i++), BretonDemo.ObjectTypes.SpindleInput);
            item.GetVariable("Spindle").Value = spindleNode.NodeId;
            item.ModellingRule = NamingRuleType.Mandatory;
            uiContainer.Add(item);
		}
    }

    void InitializeSpindlesPens(IUAObject pens, IUANode checkboxContainer)
    {
        foreach (var child in pens.Children.OfType<TrendPen>())
            child.Delete();

        RemoveUIItemChildren((Item)checkboxContainer);

        int i = 0;

        var spindlesContainer = polishingMachine.Spindles.GetObject("SpindlesContainer");

        foreach (var spindle in spindlesContainer.Children.OfType<Spindle>())
        {
            var pen = InformationModel.MakeVariable<TrendPen>("SpindlePen " + i, OpcUa.DataTypes.BaseDataType);
            pen.SetDynamicLink(spindle.Process.GetVariable("AbrasiveLevel"));
            pen.Color = new Color(255, (byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256));
            pen.Thickness = 3;

            pens.Add(pen);

            if (checkboxContainer != null)
            {
                var penCheckBox = InformationModel.MakeObject<CheckBox>("PenCheckbox" + (i));
                penCheckBox.Text = "Spindle " + i;
                penCheckBox.TextColor = pen.Color;
                penCheckBox.TopMargin = 5;
                checkboxContainer.Add(penCheckBox);

                penCheckBox.CheckedVariable.SetDynamicLink(pen.EnabledVariable);
                var dataBindNode = penCheckBox.CheckedVariable.Refs.GetNode("DynamicLink");
                if (dataBindNode != null)
                {
                    var dataBind = (DataBind)dataBindNode;
                    dataBind.Mode = DynamicLinkMode.ReadWrite;
                }
            }

            ++i;
        }
    }

    int GetActualNumberOfSpindles()
    {
        var spindlesContainer = polishingMachine.Spindles.GetObject("SpindlesContainer");
        return spindlesContainer.Children.OfType<Spindle>().Count();
    }

    public override void Stop()
    {
        var countVariable = polishingMachine.Spindles.GetVariable("Count");
        countVariable.VariableChange -= CountVariable_VariableChange;
    }

    private PolishingMachine polishingMachine;
	private Random random = new Random();
    private IUAObject plcLogicObject;
}
