using Fluens.AppCore.ViewModels.Settings.History;
using System.Collections.ObjectModel;

namespace Fluens.AppCore.Services;

public class HistoryPage
{
    public required ReadOnlyCollection<HistoryEntryViewModel> Items { get; init; }
    public DateTime? NextLastDate { get; init; }
    public int? NextLastId { get; init; }
}