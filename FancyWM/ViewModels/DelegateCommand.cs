using System;
using System.Windows.Input;

namespace FancyWM.ViewModels
{
    class DelegateCommand(Action<object?> executeDelegate, Predicate<object?>? canExecuteDelegate = null) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public Action<object?> ExecuteDelegate { get; } = executeDelegate ?? throw new ArgumentNullException(nameof(executeDelegate));
        public Predicate<object?> CanExecuteDelegate { get; } = canExecuteDelegate ?? (_ => true);

        public static DelegateCommand Create<T>(Action<T> executeDelegate) => new(
            obj => executeDelegate((T)obj! ?? throw new ArgumentNullException()));
        public static DelegateCommand Create<T>(Action<T> executeDelegate, Predicate<T> canExecuteDelegate) => new(
            obj => executeDelegate((T)obj! ?? throw new ArgumentNullException()),
            obj => canExecuteDelegate((T)obj! ?? throw new ArgumentNullException()));

        public void Execute(object? parameter)
        {
            ExecuteDelegate(parameter);
        }

        public bool CanExecute(object? parameter)
        {
            return CanExecuteDelegate(parameter);
        }
    }
}
