#region StandardUsing
using System;
using QPlatform.Core;
using QPlatform.CoreBase;
using QPlatform.HMIProject;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using QPlatform.NetLogic;
using QPlatform.UI;
using QPlatform.OPCUAServer;
using QPlatform.Recipe;
using Store = QPlatform.Store;
using System.Linq;
using System.Threading;
using QPlatform.Report;
using QPlatform.EventLogger;
#endregion

/*
 * NetLogic for creating or saving a recipe. 
 * This logic is connected to the operation of the save button of the recipe editor.
 */

public class RecipeEditorSaveButtonLogic : QPlatform.NetLogic.BaseNetLogic
{
	public override void Start()
	{
	}

	public override void Stop()
	{
	}

	[ExportMethod]
	public void CreateOrSaveRecipe(string recipeName, NodeId recipeSchema)
	{
		try
		{
			if (String.IsNullOrEmpty(recipeName))
				throw new Exception("Recipe name is empty");

			if(recipeSchema == null || recipeSchema == NodeId.Empty)
				throw new Exception("Recipe schema nodeId is empty");

			var schema = GetRecipeSchema(recipeSchema);
			var store = GetRecipeStore(schema);
			var editModel = GetEditModel(schema);

			if (RecipeExistsInStore(store, schema, recipeName))
			{
				// Save Recipe
				schema.CopyToStoreRecipe(editModel.NodeId, recipeName, CopyErrorPolicy.BestEffortCopy);
				SetOutputMessage("Recipe " + recipeName + " saved");
				return;
			}

			// Create recipe
			schema.CreateStoreRecipe(recipeName);

			// Save Recipe
			schema.CopyToStoreRecipe(editModel.NodeId, recipeName, CopyErrorPolicy.BestEffortCopy);

			var comboBox = GetComboBox();
			comboBox.Refresh();

			SetOutputMessage("Recipe " + recipeName + " created and saved");
		}
		catch (Exception e)
		{
			Log.Error("CreateOrSaveRecipe", e.Message);
			SetOutputMessage("Error saving recipe: " + e.Message);
		}
	}

	private RecipeSchema GetRecipeSchema(NodeId recipeSchemaNodeId)
	{
		// Get RecipeSchema node from its NodeId
		var recipeSchemaNode = LogicObject.Context.GetObject(recipeSchemaNodeId);
		if (recipeSchemaNode == null)
			throw new Exception("Recipe not found, nodeId: " + recipeSchemaNodeId);

		// Check that it is actually a RecipeSchema
		var schema = recipeSchemaNode as RecipeSchema;
		if (schema == null)
			throw new Exception(recipeSchemaNode.BrowseName + " is not a recipe");

		return schema;
	}

	private Store.Store GetRecipeStore(RecipeSchema schema)
	{
		// Check if the store is set
		if (schema.Store == NodeId.Empty)
			throw new Exception("Store of schema " + schema.BrowseName + " is not set");

		// Get store node
		var storeNode = LogicObject.Context.GetObject(schema.Store);
		if (storeNode == null)
			throw new Exception("Store " + schema.Store + " not found");

		// Check that it is actually a store
		var store = storeNode as Store.Store;
		if (store == null)
			throw new Exception(storeNode.BrowseName + " is not a store");

		return store;
	}

	private IUANode GetEditModel(RecipeSchema schema)
	{
		var editModel = schema.Children["EditModel"];
		if (editModel == null)
			throw new Exception("EditModel of schema " + schema.BrowseName + " not found");

		return editModel;
	}

	private bool RecipeExistsInStore(Store.Store store, RecipeSchema schema, string recipeName)
	{
		// Perform query on the store in order to check if the recipe already exists
		object[,] resultSet;
		string[] header;
		var tableName = !String.IsNullOrEmpty(schema.TableName) ? schema.TableName : schema.BrowseName;
		store.Query("SELECT * FROM \"" + tableName + "\" WHERE Name LIKE \'" + recipeName + "\'", out header, out resultSet);
		var rowCount = resultSet != null ? resultSet.GetLength(0) : 0;
		return rowCount > 0;
	}

	private ComboBox GetComboBox()
	{
		// Find ComboBox
		var comboBoxNode = Owner.Owner.Children.Get("RecipesComboBox");
		if (comboBoxNode == null)
			throw new Exception("RecipesComboBox node not found");

		// Check that it is actually a ComboBox
		ComboBox comboBox = comboBoxNode as ComboBox;
		if (comboBox == null)
			throw new Exception(comboBoxNode.BrowseName + " is not a comboBox");

		return comboBox;
	}

	private void SetOutputMessage(string message)
	{
		var outputMessageLabelNode = Owner.Owner.Children.Get("OutputMessage");
		if(outputMessageLabelNode == null)
			throw new Exception("OutputMessage label not found");

		var outputMessageLogic1 = outputMessageLabelNode.Children.Get<IUAObject>("RecipeEditorOutputMessageLogic");
		outputMessageLogic1.ExecuteMethod("SetOutputMessage", new object[] { message });
	}
}
