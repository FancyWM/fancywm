using System;
using System.Windows.Input;

namespace FancyWM.Utilities
{
    internal class DelegateCommand : ICommand
    {
        private readonly Action<object?, Action> m_execute;
        private readonly Predicate<object?> m_canExecute;

        public event EventHandler? CanExecuteChanged;

        public DelegateCommand(Action<object?> execute)
        {
            m_execute = (parameter, _) => execute(parameter);
            m_canExecute = _ => true;
        }

        public DelegateCommand(Action<object?, Action> execute, Predicate<object?> canExecute)
        {
            m_execute = execute;
            m_canExecute = canExecute;
        }

        public bool CanExecute(object? parameter)
        {
            return m_canExecute(parameter);
        }

        public void Execute(object? parameter)
        {
            m_execute(parameter, () => CanExecuteChanged?.Invoke(this, new EventArgs()));
        }
    }

    internal class DelegateCommand<TParam> : ICommand
    {
        private readonly Action<TParam, Action> m_execute;
        private readonly Predicate<TParam> m_canExecute;

        public event EventHandler? CanExecuteChanged;

        public DelegateCommand(Action<TParam> execute)
        {
            m_execute = (parameter, _) => execute(parameter);
            m_canExecute = _ => true;
        }

        public DelegateCommand(Action<TParam, Action> execute, Predicate<TParam> canExecute)
        {
            m_execute = execute;
            m_canExecute = canExecute;
        }

        public bool CanExecute(object? parameter)
        {
            if (parameter == null)
                throw new ArgumentNullException(nameof(parameter));
            return m_canExecute((TParam)parameter);
        }

        public void Execute(object? parameter)
        {
            if (parameter == null)
                throw new ArgumentNullException(nameof(parameter));
            m_execute((TParam)parameter, () => CanExecuteChanged?.Invoke(this, new EventArgs()));
        }
    }
}
