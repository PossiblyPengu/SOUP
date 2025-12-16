using CommunityToolkit.Mvvm.ComponentModel;

namespace SOUP.ViewModels;

/// <summary>
/// Base class for all ViewModels in the application.
/// </summary>
/// <remarks>
/// <para>
/// This abstract class inherits from <see cref="ObservableObject"/> provided by
/// CommunityToolkit.Mvvm, which implements <see cref="System.ComponentModel.INotifyPropertyChanged"/>
/// and provides property change notification infrastructure.
/// </para>
/// <para>
/// All ViewModels should inherit from this class to ensure consistent behavior
/// and to allow for application-wide ViewModel functionality to be added later.
/// </para>
/// </remarks>
public abstract class ViewModelBase : ObservableObject
{
}
