#region StandardUsing
using System;
using QPlatform.Core;
using QPlatform.CoreBase;
using QPlatform.HMIProject;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using QPlatform.NetLogic;
using QPlatform.OPCUAServer;
using QPlatform.Recipe;
using QPlatform.UI;
using QPlatform.Store;
using QPlatform.SQLiteStore;
using QPlatform.Report;
using QPlatform.EventLogger;
#endregion

/*
 * Logic linked to the ComboBox of the recipe editor and allows you to copy the selected recipe to the EditModel.
 */

public class RecipesComboBoxLogic : QPlatform.NetLogic.BaseNetLogic
{
	public override void Start()
	{
		var comboBox = (ComboBox)Owner;
		comboBox.SelectedValueVariable.VariableChange += SelectedValueVariable_VariableChange;
	}

	private void SelectedValueVariable_VariableChange(object sender, VariableChangeEventArgs e)
	{
		var comboBox = (ComboBox)Owner;
		if (comboBox.SelectedValue == null)
			return;

		var recipeSchemaEditor = Owner.Owner;
		var recipeSchemaVariable = recipeSchemaEditor.GetVariable("RecipeSchema");
		if (recipeSchemaVariable == null)
			return;

		var recipeSchemaNodeId = (NodeId)recipeSchemaVariable.Value.Value;

		var context = LogicObject.Context;
		var recipeSchemaObject = (RecipeSchema)context.GetObject(recipeSchemaNodeId);
		if (recipeSchemaObject == null)
			return;

		var editModelNode = recipeSchemaObject.GetObject("EditModel");
		if (editModelNode == null)
			return;

		var recipeNameLocalizedText = comboBox.SelectedValue as LocalizedText;
		if (recipeNameLocalizedText == null)
			return;

		var recipeName = recipeNameLocalizedText.Text;

		recipeSchemaObject.CopyFromStoreRecipe(recipeName, editModelNode.NodeId, CopyErrorPolicy.BestEffortCopy);
	}

	public override void Stop()
	{
		var comboBox = (ComboBox)Owner;
		comboBox.SelectedValueVariable.VariableChange -= SelectedValueVariable_VariableChange;
	}
}
