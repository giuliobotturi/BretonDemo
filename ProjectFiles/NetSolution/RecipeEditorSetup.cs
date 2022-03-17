#region StandardUsing
using System;
using QPlatform.Core;
using QPlatform.CoreBase;
using QPlatform.HMIProject;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using QPlatform.NetLogic;
using QPlatform.Store;
using QPlatform.UI;
using QPlatform.OPCUAServer;
using QPlatform.Recipe;
#endregion
using System.Linq;
using System.Collections.Generic;
using UAManagedCore.Logging;
using QPlatform.Report;
using QPlatform.EventLogger;

/*
 * Design time script for creating the recipe editor's user interface.
 */

public class RecipeEditorSetup : QPlatform.NetLogic.BaseNetLogic
{
	[ExportMethod]
	public void Setup()
	{
		try
		{
			schema = GetRecipeSchema();

			var schemaEntries = GetSchemaEntries();

			var controlsContainer = GetControlsContainer();
			CleanUI(controlsContainer);

			ConfigureComboBox();

			target = GetTargetNode();

			SetTargetNodeToButtonMethod("ApplyButton", "DestinationNode");
			SetTargetNodeToButtonMethod("LoadButton", "SourceNode");

			BuildUIFromSchemaRecursive(schemaEntries, controlsContainer);

            schema.Copy(target.NodeId, schema.GetObject("EditModel").NodeId, CopyErrorPolicy.BestEffortCopy);
		}
		catch (Exception e)
		{
			Log.Error("RecipesEditor", e.Message);
		}
	}

	private RecipeSchema GetRecipeSchema()
	{
		var recipeSchemaPtr = Owner.Children.Get<IUAVariable>("RecipeSchema");
		if (recipeSchemaPtr == null)
			throw new Exception("RecipeSchema variable not found");

		var nodeId = (NodeId) recipeSchemaPtr.Value;
		if (nodeId == null)
			throw new Exception("RecipeSchema not set");

		var recipeSchema = LogicObject.Context.GetObject(nodeId);
		if (recipeSchema == null)
			throw new Exception("Recipe not found");

		// Check if it has correct type
		var schema = recipeSchema as RecipeSchema;
		if(schema == null)
			throw new Exception(recipeSchema.BrowseName + " is not a recipe");

		return schema;
	}

	private ChildNodeCollection GetSchemaEntries()
	{
		var rootNode = schema.Children.Get("Root");
		if (rootNode == null)
			throw new Exception("Root node not found in recipe schema " + schema.BrowseName);

		var schemaEntries = rootNode.Children;
		if (schemaEntries.Count == 0)
			throw new Exception("Recipe schema " + schema.BrowseName + " has no entries");

		return schemaEntries;
	}

	private QPlatform.UI.ColumnLayout GetControlsContainer()
	{
		var scrollView = Owner.Children["ScrollView"];
		if (scrollView == null)
			throw new Exception("ScrollView not found");

		var controlsContainer = scrollView.Children.Get<QPlatform.UI.ColumnLayout>("ColumnLayout");
		if (controlsContainer == null)
			throw new Exception("ColumnLayout not found");

		return controlsContainer;
	}

	private void CleanUI(QPlatform.UI.ColumnLayout controlsContainer)
	{
		controlsContainer.Children.Clear();
		controlsContainer.Height = 0;
		controlsContainer.HorizontalAlignment = HorizontalAlignment.Stretch;
	}

	private void ConfigureComboBox()
	{
		// Set store as model for ComboBox
		var recipesComboBox = Owner.Children.Get<ComboBox>("RecipesComboBox");
		if (recipesComboBox == null)
			throw new Exception("RecipesComboBox not found");

		if (schema.Store == null)
			throw new Exception("Store of schema " + schema.BrowseName + " is not set");

        recipesComboBox.Model = schema.Store;

		// Set query of combobox with correct table name
		var tableName = !String.IsNullOrEmpty(schema.TableName) ? schema.TableName : schema.BrowseName;
		recipesComboBox.Query = "SELECT * FROM \"" + tableName + "\"";
	}

	private IUANode GetTargetNode()
	{
		var targetNode = schema.Children.Get<IUAVariable>("TargetNode");
		if (targetNode == null)
			throw new Exception("Target Node variable not found in schema " + schema.BrowseName);

		if ((NodeId)targetNode.Value == NodeId.Empty)
			throw new Exception("Target Node variable not set in schema " + schema.BrowseName);

		target = LogicObject.Context.GetNode(targetNode.Value);
		if (target == null)
			throw new Exception("Target " + targetNode.Value + " not found");

		return target;
	}

	private void SetTargetNodeToButtonMethod(string buttonName, string argumentName)
	{
		var applyButton = Owner.Children[buttonName];
		if (applyButton == null)
			throw new Exception(buttonName + "not found");

		var argumentNode = GetInputArgument(applyButton, 1, argumentName);
		argumentNode.Value = target.NodeId;
	}

	private void BuildUIFromSchemaRecursive(IEnumerable<IUANode> entries, Item controlsContainer, string currentPath = "")
	{
		foreach (var entry in entries)
		{
			string path = (currentPath == "" ? "" : currentPath + "/") + entry.BrowseName;

			if (entry.NodeClass == NodeClass.Variable)
			{
				var control = BuildControl((IUAVariable)entry, path);
				controlsContainer.Height += control.Height;
				controlsContainer.Children.Add(control);
			}

			if (entry.Children.Count > 0)
				BuildUIFromSchemaRecursive(entry.Children, controlsContainer, path);
		}
	}

	private Item BuildControl(IUAVariable variable, string path)
	{
		var dataType = variable.Context.GetDataType(variable.DataType);
		if(dataType.IsSubTypeOf(OpcUa.DataTypes.Integer))
			return BuildSpinbox(variable, path);
		if (variable.DataType == OpcUa.DataTypes.Boolean)
            return BuildSwitch(variable, path);
		else
            return BuildTextBox(variable, path);
	}

	private Item BuildControlPanel(IUAVariable variable, string path)
	{
		var panel = InformationModel.MakeObject<QPlatform.UI.Panel>(variable.BrowseName);
		panel.Height = 40;
		panel.HorizontalAlignment = HorizontalAlignment.Stretch;

		var label = InformationModel.MakeObject<QPlatform.UI.Label>("Path");
		label.Text = path;
		label.LeftMargin = 20;
		label.VerticalAlignment = VerticalAlignment.Center;
		panel.Children.Add(label);

        var tokens = path.Split('/');
        var node = target;
        foreach(var token in tokens)
        {
			if (node == null)
			{
				Log.Error("RecipesEditor", "Node " + path + " not found in target " + target.BrowseName);
				continue;
			}

            node = node.Children.Get(token);
        }

        var variableTarget = (IUAVariable)node;

        var label2 = InformationModel.MakeObject<QPlatform.UI.Label>("CurrentValue");
        label2.TextVariable.SetDynamicLink(variableTarget);
        label2.VerticalAlignment = VerticalAlignment.Center;
        label2.HorizontalAlignment = HorizontalAlignment.Right;
        panel.Children.Add(label2);

        return panel;
	}

	private Item BuildSpinbox(IUAVariable variable, string path)
	{
		var panel = BuildControlPanel(variable, path);

		var spinbox = InformationModel.MakeObject<SpinBox>("SpinBox");
		spinbox.VerticalAlignment = VerticalAlignment.Center;
		spinbox.HorizontalAlignment = HorizontalAlignment.Right;
		spinbox.RightMargin = 80;
		spinbox.Width = 80;

		MakeDataBind(spinbox.Children.Get<IUAVariable>("Value"), "{" + schema.BrowseName + "}@Jump/" + path);
		panel.Children.Add(spinbox);

		return panel;
	}

    private Item BuildTextBox(IUAVariable variable, string path)
    {
        var panel = BuildControlPanel(variable, path);

        var textbox = InformationModel.MakeObject<TextBox>("Textbox");
        textbox.VerticalAlignment = VerticalAlignment.Center;
        textbox.HorizontalAlignment = HorizontalAlignment.Right;
        textbox.RightMargin = 80;
        textbox.Width = 80;

        MakeDataBind(textbox.Children.Get<IUAVariable>("Text"), "{" + schema.BrowseName + "}@Jump/" + path);
        panel.Children.Add(textbox);

        return panel;
    }

    private Item BuildSwitch(IUAVariable variable, string path)
	{
		var panel = BuildControlPanel(variable, path);

		var switchControl = InformationModel.MakeObject<Switch>("Switch");
		switchControl.VerticalAlignment = VerticalAlignment.Center;
		switchControl.HorizontalAlignment = HorizontalAlignment.Right;
		switchControl.RightMargin = 80;
		switchControl.Width = 60;

		MakeDataBind(switchControl.Children.Get<IUAVariable>("Checked"), "{" + schema.BrowseName + "}@Jump/" + path);
		panel.Children.Add(switchControl);

		return panel;
	}

	private void MakeDataBind(IUAVariable parent, IUAVariable variable)
	{
		var dataBind = InformationModel.MakeVariable<DataBind>("DataBind", QPlatform.Core.DataTypes.NodePath);
		dataBind.Value = DataBindPath.MakeAbsolutePath(variable);
		dataBind.Mode = DynamicLinkMode.ReadWrite;
		//parent.SetDataBind(dataBind);

        parent.Refs.AddReference(QPlatform.CoreBase.ReferenceTypes.HasDataBind, dataBind);

    }

	private void MakeDataBind(IUAVariable parent, string path)
	{
		var dataBind = InformationModel.MakeVariable<DataBind>("DataBind", QPlatform.Core.DataTypes.NodePath);
		dataBind.Value = path;
		dataBind.Mode = DynamicLinkMode.ReadWrite;
		//parent.SetDataBind(dataBind);

        parent.Refs.AddReference(QPlatform.CoreBase.ReferenceTypes.HasDataBind, dataBind);
    }

	private IUAVariable GetInputArgument(IUANode node, int methodIndex, string argumentName)
	{
		return node.Children["EventHandler1"].Children["MethodsToCall"]
										 .Children["MethodContainer" + methodIndex].Children["InputArguments"]
										 .Children.Get<IUAVariable>(argumentName);
	}

	RecipeSchema schema;
    IUANode target;
}
