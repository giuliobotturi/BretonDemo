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
using QPlatform.Report;
using QPlatform.EventLogger;
#endregion

/*
 * NetLogic that creates the model for the combo box of the Locales extrapolating them from those defined on the project.
 */

public class LocaleComboBoxLogic : QPlatform.NetLogic.BaseNetLogic
{
    public override void Start()
    {
        var localeCombo = (ComboBox)Owner;

        var projectLocales = (string[])Project.Current.GetVariable("Locales").Value;
        var modelLocales = InformationModel.MakeObject("Locales");
        modelLocales.Children.Clear();

        foreach (var locale in projectLocales)
        {
            var language = InformationModel.MakeVariable(locale, OpcUa.DataTypes.String);
            language.Value = locale;
            modelLocales.Children.Add(language);
        }

        LogicObject.Children.Add(modelLocales);
        localeCombo.Model = modelLocales.NodeId;
    }

    public override void Stop()
    {
    }
}
