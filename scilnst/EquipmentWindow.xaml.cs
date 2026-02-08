using Microsoft.EntityFrameworkCore;
using scilnst.Data;
using scilnst.Models;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace scilnst
{
    public partial class EquipmentWindow : Window
    {
        private readonly SciInstContext _context;
        private bool _canSeeStatus;

        public EquipmentWindow()
        {
            InitializeComponent();

            _context = new SciInstContext();
            InitializeWindow();
        }

        private void InitializeWindow()
        {
            LoadUserInfo();
            LoadEquipmentData();
        }

        private void LoadUserInfo()
        {
            var currentUser = MainWindow.User;
            txtUserInfo.Text = $"Пользователь: {currentUser.FullName}";
            Title = $"Список оборудования - {currentUser.FullName}";
        }

        private void LoadEquipmentData()
        {
            try
            {
                var equipmentItems = GetFilteredEquipment()
                    .Select(CreateEquipmentViewModel)
                    .ToList();

                lvEquipment.ItemsSource = equipmentItems;
                UpdateEquipmentCount(equipmentItems.Count);
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Ошибка загрузки оборудования: {ex.Message}");
            }
        }

        private IQueryable<Equipment> GetFilteredEquipment()
        {
            var query = _context.Equipment
                .Include(e => e.Room)
                    .ThenInclude(r => r.Department)
                .Include(e => e.Department)
                .Where(e => !e.IsArchived);

            SetUserPermissions();
            query = ApplyRoleBasedFilter(query);

            return query;
        }

        private void SetUserPermissions()
        {
            var userPosition = MainWindow.User.PositionTitle?.ToLower();
            _canSeeStatus = userPosition == "заведующий лабораторией" ||
                           userPosition == "администратор бд";
        }

        private IQueryable<Equipment> ApplyRoleBasedFilter(IQueryable<Equipment> baseQuery)
        {
            var user = MainWindow.User;

            if (user.IsGuest)
                return FilterForGuest(baseQuery);

            if (IsLabStaff(user.PositionTitle))
                return FilterForLabStaff(baseQuery, user.DepartmentID);

            if (IsDepartmentHead(user.PositionTitle))
                return FilterForDepartmentHead(baseQuery, user.DepartmentID);

            return baseQuery;
        }

        private static IQueryable<Equipment> FilterForGuest(IQueryable<Equipment> query)
        {
            return query.Where(e =>
                (e.Department != null && e.Department.ShortName.ToLower().Trim() == "столовая") ||
                (e.Room != null && e.Room.Department.ShortName.ToLower().Trim() == "столовая")
            );
        }

        private static IQueryable<Equipment> FilterForLabStaff(IQueryable<Equipment> query, int? departmentId)
        {
            return query.Where(e =>
                (e.Room != null && e.Room.DepartmentId == departmentId) ||
                (e.Department != null && e.DepartmentId == departmentId)
            );
        }

        private static IQueryable<Equipment> FilterForDepartmentHead(IQueryable<Equipment> query, int? departmentId)
        {
            return query.Where(e =>
                (e.Room != null && e.Room.DepartmentId == departmentId) ||
                (e.Department != null && e.DepartmentId == departmentId)
            );
        }

        private EquipmentViewModel CreateEquipmentViewModel(Equipment equipment)
        {
            return new EquipmentViewModel
            {
                EquipmentId = equipment.EquipmentId,
                Name = equipment.Name,
                Description = equipment.Description,
                Photo = GetImagePath(equipment.PhotoPath),
                Room = equipment.Room?.RoomNumber ?? string.Empty,
                Department = GetDepartmentName(equipment),
                StatusText = GetEquipmentStatus(equipment),
                StatusColor = GetStatusColor(equipment),
                RegistrationDate = equipment.RegistrationDate,
                ServiceLifeYears = equipment.ServiceLifeYears,
                InventoryNumber = equipment.InventoryNumber,
                Weight = equipment.Weight
            };
        }

        private string GetImagePath(string photoFileName)
        {
            if (!string.IsNullOrEmpty(photoFileName))
            {
                var imagePath = Path.Combine("Images", photoFileName);
                var fullPath = Path.Combine(Directory.GetCurrentDirectory(), imagePath);
                if (File.Exists(photoFileName))
                    return $"Image/{photoFileName}";
            }

            return GetDefaultImagePath();
        }

        private string GetDefaultImagePath()
        {
            var stubPath = Path.Combine(Directory.GetCurrentDirectory(), "Images", "stub.jpg");
            return File.Exists(stubPath) ? "/Images/stub.jpg" : null;
        }

        private string GetDepartmentName(Equipment equipment)
        {
            return equipment.Department?.FullName ??
                   equipment.Room?.Department?.FullName ??
                   string.Empty;
        }

        private string GetEquipmentStatus(Equipment equipment)
        {
            if (!_canSeeStatus)
                return string.Empty;

            var currentDate = DateTime.Now;
            var endDate = equipment.RegistrationDate.AddYears(equipment.ServiceLifeYears);
            var isInStorage = IsEquipmentInStorage(equipment);

            if (endDate < currentDate && !isInStorage)
                return "На списание";

            if (endDate.Year == currentDate.Year)
                return "Срок службы истекает в этом году";

            return $"Срок службы до: {endDate:yyyy}";
        }

        private Brush GetStatusColor(Equipment equipment)
        {
            if (!_canSeeStatus)
                return Brushes.Transparent;

            var currentDate = DateTime.Now;
            var endDate = equipment.RegistrationDate.AddYears(equipment.ServiceLifeYears);
            var isInStorage = IsEquipmentInStorage(equipment);

            if (endDate < currentDate && !isInStorage)
                return CreateBrush("#E32636");

            if (endDate.Year == currentDate.Year)
                return CreateBrush("#FFA500");

            return Brushes.LightGreen;
        }

        private static bool IsEquipmentInStorage(Equipment equipment)
        {
            return equipment.Department != null &&
                   equipment.Department.ShortName.ToLower().Contains("склад");
        }

        private static Brush CreateBrush(string hexColor)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexColor));
        }

        private static bool IsLabStaff(string position)
        {
            var lowerPosition = position?.ToLower();
            return lowerPosition == "лаборант" || lowerPosition == "техник";
        }

        private static bool IsDepartmentHead(string position)
        {
            var lowerPosition = position?.ToLower();
            return lowerPosition == "заведующий лабораторией" ||
                   lowerPosition == "заведующий складом";
        }

        private void UpdateEquipmentCount(int count)
        {
            txtEquipmentCount.Text = $"Найдено оборудования: {count}";
        }

        private void ShowErrorMessage(string message)
        {
            MessageBox.Show(message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateToMainWindow();
        }

        private void NavigateToMainWindow()
        {
            var mainWindow = new MainWindow();
            mainWindow.Show();
            Close();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if (Application.Current.Windows.Count == 0)
                Application.Current.Shutdown();
        }
    }

    public class EquipmentViewModel
    {
        public int EquipmentId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Photo { get; set; }
        public string Room { get; set; }
        public string Department { get; set; }
        public string StatusText { get; set; }
        public Brush StatusColor { get; set; }
        public DateTime RegistrationDate { get; set; }
        public int ServiceLifeYears { get; set; }
        public string InventoryNumber { get; set; }
        public decimal Weight { get; set; }
    }
}