namespace Glovelly.Api.Models;

public enum CalendarSyncWorkItemReason
{
    GigCreated,
    GigUpdated,
    GigCancelled,
    GigDeleted,
    SettingsChanged,
    ConnectionChanged,
    ManualSync,
    CalendarRecreated
}
