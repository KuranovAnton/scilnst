using Microsoft.EntityFrameworkCore;
using System.Windows;
using scilnst.Data;
using scilnst.Models;

namespace scilnst
{
    public partial class MainWindow : Window
    {
        private const string GuestUserName = "Гость";
        private const string GuestPosition = "Гость";

        public static CurrentUser User { get; private set; } = new CurrentUser();

        public MainWindow()
        {
            InitializeComponent();
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            AttemptUserLogin();
        }

        private void AttemptUserLogin()
        {
            try
            {
                var credentials = GetUserCredentials();

                using (var context = new SciInstContext())
                {
                    var authenticatedEmployee = FindEmployeeByCredentials(context, credentials);

                    if (authenticatedEmployee != null)
                    {
                        InitializeAuthenticatedUser(authenticatedEmployee);
                        NavigateToEquipmentWindow();
                    }
                    else
                    {
                        ShowLoginErrorMessage();
                    }
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Ошибка при входе: {ex.Message}");
            }
        }

        private UserCredentials GetUserCredentials()
        {
            return new UserCredentials
            {
                Login = txtLogin.Text.Trim(),
                Password = txtPassword.Password
            };
        }

        private Employee FindEmployeeByCredentials(SciInstContext context, UserCredentials credentials)
        {
            return context.Employees
                .Include(e => e.Position)
                .Include(e => e.Department)
                .FirstOrDefault(e =>
                    e.Username == credentials.Login &&
                    e.Password == credentials.Password);
        }

        private void InitializeAuthenticatedUser(Employee employee)
        {
            User = new CurrentUser
            {
                EmployeeID = employee.EmployeeId,
                FullName = FormatEmployeeFullName(employee),
                DepartmentID = employee.DepartmentId,
                PositionTitle = employee.Position?.Title ?? string.Empty,
                IsGuest = false,
                DepartmentShortName = employee.Department?.ShortName ?? string.Empty
            };
        }

        private string FormatEmployeeFullName(Employee employee)
        {
            return $"{employee.LastName} {employee.FirstName} {employee.MiddleName}";
        }

        private void GuestButton_Click(object sender, RoutedEventArgs e)
        {
            InitializeGuestUser();
            NavigateToEquipmentWindow();
        }

        private void InitializeGuestUser()
        {
            User = new CurrentUser
            {
                FullName = GuestUserName,
                IsGuest = true,
                PositionTitle = GuestPosition
            };
        }

        private void NavigateToEquipmentWindow()
        {
            var equipmentWindow = new EquipmentWindow();
            equipmentWindow.Show();
            Close();
        }

        private void ShowLoginErrorMessage()
        {
            MessageBox.Show(
                "Неверный логин или пароль",
                "Ошибка входа",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
        }

        private void ShowErrorMessage(string message)
        {
            MessageBox.Show(
                message,
                "Ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }

        private class UserCredentials
        {
            public string Login { get; set; }
            public string Password { get; set; }
        }
    }
}