using System;
using QPlatform.Core;
using CoreBase = QPlatform.CoreBase;
using HMIProject = QPlatform.HMIProject;
using UAManagedCore;
using QPlatform.UI;
using QPlatform.Report;
using QPlatform.EventLogger;

/*
 * Library script that updates a model variable with the current time every one second.
 */

public class ClockLogic : QPlatform.NetLogic.BaseNetLogic
{
    public override void Start()
    {
        periodicTask = new PeriodicTask(UpdateTime, 1000, LogicObject);
        periodicTask.Start();
    }

    public override void Stop()
    {
        periodicTask.Dispose();
        periodicTask = null;
    }

    private void UpdateTime()
    {
        var timeVar = LogicObject.Children.Get<IUAVariable>("Time");
        timeVar.Value = DateTime.UtcNow;
    }

    private PeriodicTask periodicTask;
}
