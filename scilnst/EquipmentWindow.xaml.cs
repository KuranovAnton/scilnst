using Microsoft.EntityFrameworkCore;
using scilnst.Data;
using scilnst.Models;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace scilnst
{
    public partial class EquipmentWindow : Window, INotifyPropertyChanged
    {
        private readonly SciInstContext _context;
        private bool _canSeeStatus;
        private bool _canUseFilters;
        private bool _canAddEdit;
        private bool _canViewDetails;
        private static bool _isEditWindowOpen;

        private FilterSortOptions _currentOptions;
        private List<DepartmentFilterItem> _departmentFilters;

        public event PropertyChangedEventHandler? PropertyChanged;

        public EquipmentWindow()
        {
            InitializeComponent();

            _context = new SciInstContext();
            _currentOptions = new FilterSortOptions();
            _isEditWindowOpen = false;

            InitializeDataContext();
            LoadInitialData();
        }


        public bool CanSeeStatus
        {
            get => _canSeeStatus;
            set => SetField(ref _canSeeStatus, value);
        }

        public bool CanUseFilters
        {
            get => _canUseFilters;
            set => SetField(ref _canUseFilters, value);
        }

        public bool CanAddEdit
        {
            get => _canAddEdit;
            set => SetField(ref _canAddEdit, value);
        }

        public bool CanViewDetails
        {
            get => _canViewDetails;
            set => SetField(ref _canViewDetails, value);
        }

                private void InitializeDataContext()
        {
            DataContext = this;
        }

        private void LoadInitialData()
        {
            LoadUserInfo();
            LoadDepartments();
            LoadEquipment();
        }

        private void LoadUserInfo()
        {
            UpdateUserDisplay();
            SetUserPermissions();
        }

        private void UpdateUserDisplay()
        {
            txtUserInfo.Text = $"{MainWindow.User.FullName}";
            Title = $"Список оборудования - {MainWindow.User.FullName}";
        }

        private void SetUserPermissions()
        {
            var position = MainWindow.User.PositionTitle;

            CanSeeStatus = IsPositionIn(position, "заведующий лабораторией", "администратор бд");
            CanUseFilters = IsPositionIn(position, "инженер", "администратор бд", "администратор");
            CanAddEdit = IsPositionIn(position, "администратор бд", "администратор",
                "заведующий лабораторией", "заведующий складом");
            CanViewDetails = IsPositionIn(position, "техник", "инженер") || CanAddEdit;
        }

        private static bool IsPositionIn(string position, params string[] allowedPositions)
        {
            return allowedPositions.Contains(position);
        }

        private void LoadDepartments()
        {
            try
            {
                var departments = FetchDepartmentsFromDatabase();
                _departmentFilters = BuildDepartmentFilterList(departments);

                ConfigureDepartmentFilterComboBox();
            }
            catch (Exception exception)
            {
                ShowErrorMessage("Ошибка загрузки подразделений", exception);
            }
        }

        private List<Department> FetchDepartmentsFromDatabase()
        {
            return _context.Departments
                .OrderBy(department => department.FullName)
                .ToList();
        }

        private List<DepartmentFilterItem> BuildDepartmentFilterList(List<Department> departments)
        {
            var filterItems = new List<DepartmentFilterItem>
            {
                CreateAllDepartmentsFilterItem()
            };

            filterItems.AddRange(departments.Select(CreateDepartmentFilterItem));

            return filterItems;
        }

        private static DepartmentFilterItem CreateAllDepartmentsFilterItem()
        {
            return new DepartmentFilterItem
            {
                DepartmentId = null,
                DisplayName = "Все подразделения"
            };
        }

        private static DepartmentFilterItem CreateDepartmentFilterItem(Department department)
        {
            return new DepartmentFilterItem
            {
                DepartmentId = department.DepartmentId,
                DisplayName = department.FullName
            };
        }

        private void ConfigureDepartmentFilterComboBox()
        {
            cmbDepartmentFilter.ItemsSource = _departmentFilters;
            cmbDepartmentFilter.SelectedIndex = 0;
        }


        public void LoadEquipment()
        {
            try
            {
                var currentDate = DateTime.Now;
                var equipment = GetFilteredEquipment();
                var viewModels = CreateEquipmentViewModels(equipment, currentDate);

                lvEquipment.ItemsSource = viewModels;
            }
            catch (Exception exception)
            {
                ShowErrorMessage("Ошибка загрузки оборудования", exception);
            }
        }

        private List<Equipment> GetFilteredEquipment()
        {
            var query = BuildBaseEquipmentQuery();
            query = ApplyUserAccessFilter(query);
            query = ApplyDepartmentFilter(query);

            var equipmentList = ExecuteEquipmentQuery(query);
            equipmentList = ApplySearchFilter(equipmentList);
            equipmentList = ApplySorting(equipmentList);

            return equipmentList;
        }

        private IQueryable<Equipment> BuildBaseEquipmentQuery()
        {
            return _context.Equipment
                .Include(equipment => equipment.Room)
                    .ThenInclude(room => room.Department)
                .Include(equipment => equipment.Department)
                .Where(equipment => !equipment.IsArchived);
        }

        private IQueryable<Equipment> ApplyUserAccessFilter(IQueryable<Equipment> query)
        {
            if (MainWindow.User.IsGuest)
            {
                return query.Where(equipment =>
                    (equipment.Department != null && equipment.Department.ShortName.Trim().ToLower() == "столовая") ||
                    (equipment.Room != null && equipment.Room.Department != null &&
                     equipment.Room.Department.ShortName.Trim().ToLower() == "столовая"));
            }

            if (IsRestrictedUser())
            {
                return query.Where(equipment =>
                    (equipment.Room != null && equipment.Room.DepartmentId == MainWindow.User.DepartmentID) ||
                    (equipment.Department != null && equipment.DepartmentId == MainWindow.User.DepartmentID));
            }

            return query;
        }

        private bool IsRestrictedUser()
        {
            return !IsPositionIn(MainWindow.User.PositionTitle,
                "администратор бд", "администратор", "инженер");
        }

        private IQueryable<Equipment> ApplyDepartmentFilter(IQueryable<Equipment> query)
        {
            if (!CanUseFilters || !_currentOptions.SelectedDepartmentId.HasValue)
            {
                return query;
            }

            var departmentId = _currentOptions.SelectedDepartmentId.Value;

            return query.Where(equipment =>
                (equipment.Department != null && equipment.DepartmentId == departmentId) ||
                (equipment.Room != null && equipment.Room.DepartmentId == departmentId));
        }

        private List<Equipment> ExecuteEquipmentQuery(IQueryable<Equipment> query)
        {
            return query.ToList();
        }

        private List<Equipment> ApplySearchFilter(List<Equipment> equipmentList)
        {
            if (!CanUseFilters || string.IsNullOrWhiteSpace(_currentOptions.SearchText))
            {
                return equipmentList;
            }

            return FilterBySearchText(equipmentList);
        }

        private List<Equipment> FilterBySearchText(List<Equipment> equipmentList)
        {
            var searchWords = ParseSearchWords();

            return searchWords.Length == 0
                ? equipmentList
                : equipmentList.Where(equipment => MatchesAllSearchWords(equipment, searchWords)).ToList();
        }

        private string[] ParseSearchWords()
        {
            return _currentOptions.SearchText
                .ToLower()
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(word => word.Trim())
                .ToArray();
        }

        private bool MatchesAllSearchWords(Equipment equipment, string[] searchWords)
        {
            var searchableText = BuildSearchableText(equipment);
            return searchWords.All(word => searchableText.Contains(word));
        }

        private string BuildSearchableText(Equipment equipment)
        {
            return string.Join(" ",
                equipment.Name ?? "",
                equipment.Description ?? "",
                equipment.InventoryNumber ?? "",
                equipment.Department?.FullName ?? "",
                equipment.Room?.Department?.FullName ?? "",
                equipment.Room?.RoomNumber ?? ""
            ).ToLower();
        }

        private List<Equipment> ApplySorting(List<Equipment> equipmentList)
        {
            if (!CanUseFilters)
            {
                return equipmentList;
            }

            switch (_currentOptions.SortOrder)
            {
                case "Ascending":
                    return equipmentList.OrderBy(equipment => equipment.Weight).ToList();
                case "Descending":
                    return equipmentList.OrderByDescending(equipment => equipment.Weight).ToList();
                default:
                    return equipmentList;
            }
        }

        private List<EquipmentViewModel> CreateEquipmentViewModels(List<Equipment> equipmentList, DateTime currentDate)
        {
            return equipmentList
                .Select(equipment => CreateEquipmentViewModel(equipment, currentDate))
                .ToList();
        }

        private EquipmentViewModel CreateEquipmentViewModel(Equipment equipment, DateTime currentDate)
        {
            var viewModel = new EquipmentViewModel
            {
                EquipmentId = equipment.EquipmentId,
                Name = equipment.Name,
                Description = equipment.Description,
                PhotoImage = ImageHelper.LoadImage(equipment.PhotoPath),
                RegistrationDate = equipment.RegistrationDate,
                ServiceLifeYears = equipment.ServiceLifeYears,
                InventoryNumber = equipment.InventoryNumber,
                Weight = equipment.Weight,
                Room = GetRoomNumber(equipment),
                Department = GetDepartmentName(equipment)
            };

            if (CanSeeStatus)
            {
                UpdateStatusInfo(viewModel, equipment, currentDate);
            }

            return viewModel;
        }

        private static string GetRoomNumber(Equipment equipment)
        {
            return equipment.Room?.RoomNumber ?? string.Empty;
        }

        private static string GetDepartmentName(Equipment equipment)
        {
            return equipment.Department?.FullName ??
                   equipment.Room?.Department?.FullName ??
                   string.Empty;
        }

        private void UpdateStatusInfo(EquipmentViewModel viewModel, Equipment equipment, DateTime currentDate)
        {
            var endDate = equipment.RegistrationDate.AddYears(equipment.ServiceLifeYears);
            var isStorage = IsStorageDepartment(equipment.Department);

            viewModel.IsStorage = isStorage;
            viewModel.IsExpired = endDate < currentDate && !isStorage;

            var statusInfo = DetermineStatus(endDate, currentDate, isStorage);
            viewModel.StatusText = statusInfo.Text;
            viewModel.StatusColor = statusInfo.Color;
        }

        private bool IsStorageDepartment(Department? department)
        {
            return department != null &&
                   department.ShortName.ToLower().Contains("склад");
        }

        private (string Text, Brush Color) DetermineStatus(DateTime endDate, DateTime currentDate, bool isStorage)
        {
            if (endDate < currentDate && !isStorage)
            {
                return ("На списание", new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E32636")));
            }

            if (endDate.Year == currentDate.Year)
            {
                return ("Срок службы истекает в этом году",
                    new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFA500")));
            }

            return ($"Срок службы до: {endDate:yyyy}", Brushes.Transparent);
        }


        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!CanUseFilters)
            {
                return;
            }

            _currentOptions.SearchText = txtSearch.Text;
            LoadEquipment();
        }

        private void cmbDepartmentFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!CanUseFilters || !(cmbDepartmentFilter.SelectedItem is DepartmentFilterItem selectedItem))
            {
                return;
            }

            _currentOptions.SelectedDepartmentId = selectedItem.DepartmentId;
            LoadEquipment();
        }

        private void cmbSortOrder_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!CanUseFilters || !(cmbSortOrder.SelectedItem is ComboBoxItem selectedItem))
            {
                return;
            }

            _currentOptions.SortOrder = selectedItem.Tag as string ?? "None";
            LoadEquipment();
        }

        private void AddEquipmentButton(object sender, RoutedEventArgs e)
        {
            if (!CanAddEdit || _isEditWindowOpen)
            {
                return;
            }

            OpenEquipmentEditWindow(null);
        }

        private void lvEquipment_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (!CanViewDetails || _isEditWindowOpen)
            {
                return;
            }

            if (!(lvEquipment.SelectedItem is EquipmentViewModel selectedViewModel))
            {
                return;
            }

            var equipment = FindEquipmentById(selectedViewModel.EquipmentId);

            if (equipment != null)
            {
                OpenEquipmentEditWindow(equipment);
            }
        }

        private Equipment? FindEquipmentById(int equipmentId)
        {
            return _context.Equipment
                .Include(equipment => equipment.Room)
                .Include(equipment => equipment.Department)
                .FirstOrDefault(equipment => equipment.EquipmentId == equipmentId);
        }

        private void OpenEquipmentEditWindow(Equipment? equipment)
        {
            var editWindow = new EquipmentEditWindow(equipment, _context, MainWindow.User);
            editWindow.Closed += OnEquipmentEditWindowClosed;

            _isEditWindowOpen = true;
            editWindow.ShowDialog();
        }

        private void OnEquipmentEditWindowClosed(object? sender, EventArgs e)
        {
            _isEditWindowOpen = false;
            LoadEquipment();
        }

        private void ExitButton(object sender, RoutedEventArgs e)
        {
            var mainWindow = new MainWindow();
            mainWindow.Show();

            Close();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            CleanupResources();
        }


        private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return;
            }

            field = value;
            OnPropertyChanged(propertyName);
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private static void ShowErrorMessage(string message, Exception exception)
        {
            MessageBox.Show($"{message}: {exception.Message}");
        }

        private void CleanupResources()
        {
            _context?.Dispose();

            if (Application.Current.Windows.Count == 0)
            {
                Application.Current.Shutdown();
            }
        }


        private class FilterSortOptions
        {
            public string SearchText { get; set; } = string.Empty;
            public int? SelectedDepartmentId { get; set; }
            public string SortOrder { get; set; } = "None";
        }

        public class DepartmentFilterItem
        {
            public int? DepartmentId { get; set; }
            public string? DisplayName { get; set; }
        }

    }

    public class EquipmentViewModel : INotifyPropertyChanged
    {
        private string? _room;
        private string? _department;
        private BitmapImage? _photoImage;

        public event PropertyChangedEventHandler? PropertyChanged;


        public int EquipmentId { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? InventoryNumber { get; set; }
        public DateTime RegistrationDate { get; set; }
        public int ServiceLifeYears { get; set; }
        public decimal Weight { get; set; }
        public string? StatusText { get; set; }
        public Brush? StatusColor { get; set; }
        public bool IsStorage { get; set; }
        public bool IsExpired { get; set; }

        public BitmapImage? PhotoImage
        {
            get => _photoImage;
            set => SetField(ref _photoImage, value);
        }

        public string? Room
        {
            get => _room;
            set
            {
                if (SetField(ref _room, value))
                {
                    HasRoom = !string.IsNullOrEmpty(value);
                }
            }
        }

        public string? Department
        {
            get => _department;
            set
            {
                if (SetField(ref _department, value))
                {
                    HasDepartment = !string.IsNullOrEmpty(value);
                }
            }
        }

        public bool HasRoom { get; private set; }
        public bool HasDepartment { get; private set; }


        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

    }
}