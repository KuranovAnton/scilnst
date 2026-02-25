using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using scilnst.Data;
using scilnst.Models;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace scilnst
{
    public partial class EquipmentEditWindow : Window, INotifyPropertyChanged
    {
        private readonly SciInstContext _context;
        private readonly Equipment _equipment;
        private readonly CurrentUser _user;
        private readonly bool _isNewEquipment;

        private string _originalPhotoPath;
        private string _oldPhotoPath;
        private bool _hasChanges;

        private string _windowTitle;
        private string _equipmentName;
        private string _inventoryNumber;
        private string _description;
        private int? _departmentId;
        private int? _roomId;
        private decimal _weight;
        private int _serviceLifeYears;
        private DateTime _registrationDate;
        private string _photoPath;
        private bool _hasPhoto;
        private BitmapImage _photoImage;
        private List<Department> _departments;
        private List<Room> _rooms;

        private const int REQUIRED_WIDTH = 300;
        private const int REQUIRED_HEIGHT = 200;
        private const string REQUIRED_DIMENSIONS_MESSAGE = "Фото должно быть размером 300x200 пикселей.";
        private const long MAX_FILE_SIZE = 5 * 1024 * 1024; // 5 MB

        public event PropertyChangedEventHandler? PropertyChanged;

        public EquipmentEditWindow(Equipment? equipment, SciInstContext context, CurrentUser user)
        {
            InitializeComponent();

            _context = context;
            _user = user;
            _isNewEquipment = equipment == null;
            _equipment = equipment ?? new Equipment();
            _hasChanges = false;

            InitializeWindow();
        }
        
        private void InitializeWindow()
        {
            DataContext = this;
            DeterminePermissions();
            LoadData();
            LoadDepartments();
            LoadRooms();
        }

        private void DeterminePermissions()
        {
            var isAdmin = IsUserInRole("администратор бд", "администратор");
            var isManager = IsUserInRole("заведующий лабораторией", "заведующий складом");
            var isTechnician = IsUserInRole("техник");
            var isEngineer = IsUserInRole("инженер");

            SetEditPermissions(isAdmin, isManager);
            SetDeletePermissions(isAdmin);
            SetWindowTitle();
        }

        private bool IsUserInRole(params string[] roles)
        {
            return roles.Contains(_user.PositionTitle);
        }

        private void SetEditPermissions(bool isAdmin, bool isManager)
        {
            CanEdit = (isAdmin || isManager) && !_isNewEquipment;

            if (_isNewEquipment)
            {
                CanEdit = isAdmin || isManager;
            }

            CanSelectDepartment = isAdmin;
        }

        private void SetDeletePermissions(bool isAdmin)
        {
            if (_isNewEquipment)
            {
                CanDelete = false;
                return;
            }

            var isStorage = _equipment.Department != null &&
                           _equipment.Department.ShortName.ToLower().Contains("склад");

            var endDate = _equipment.RegistrationDate.AddYears(_equipment.ServiceLifeYears);
            var isExpired = endDate < DateTime.Now;

            CanDelete = isAdmin && (isStorage || isExpired);
        }

        private void SetWindowTitle()
        {
            if (_isNewEquipment)
            {
                WindowTitle = "Добавление оборудования";
            }
            else
            {
                WindowTitle = CanEdit ? "Редактирование оборудования" : "Просмотр оборудования";
            }
        }

        public string WindowTitle
        {
            get => _windowTitle;
            set => SetField(ref _windowTitle, value);
        }

        public string EquipmentName
        {
            get => _equipmentName;
            set => SetField(ref _equipmentName, value);
        }

        public string InventoryNumber
        {
            get => _inventoryNumber;
            set => SetField(ref _inventoryNumber, value);
        }

        public string Description
        {
            get => _description;
            set => SetField(ref _description, value);
        }

        public int? DepartmentId
        {
            get => _departmentId;
            set
            {
                if (_departmentId != value)
                {
                    _departmentId = value;
                    OnPropertyChanged();
                    RoomId = 0;
                    LoadRooms();
                }
            }
        }

        public int? RoomId
        {
            get => _roomId;
            set => SetField(ref _roomId, value);
        }

        public decimal Weight
        {
            get => _weight;
            set => SetField(ref _weight, value);
        }

        public int ServiceLifeYears
        {
            get => _serviceLifeYears;
            set => SetField(ref _serviceLifeYears, value);
        }

        public DateTime RegistrationDate
        {
            get => _registrationDate;
            set => SetField(ref _registrationDate, value);
        }

        public BitmapImage? PhotoImage
        {
            get => _photoImage;
            private set => SetField(ref _photoImage, value);
        }

        public string? PhotoPath
        {
            get => _photoPath;
            set
            {
                if (_photoPath != value)
                {
                    _photoPath = value;
                    OnPropertyChanged();
                    UpdatePhotoImage(value);
                    HasPhoto = !string.IsNullOrEmpty(value) && !value.Contains("stub.jpg");
                }
            }
        }

        public bool HasPhoto
        {
            get => _hasPhoto;
            private set => SetField(ref _hasPhoto, value);
        }

        public List<Department> Departments
        {
            get => _departments;
            private set => SetField(ref _departments, value);
        }

        public List<Room> Rooms
        {
            get => _rooms;
            private set => SetField(ref _rooms, value);
        }

        public bool CanEdit { get; private set; }
        public bool IsReadOnly => !CanEdit;
        public bool CanSelectDepartment { get; private set; }
        public bool CanDelete { get; private set; }
        public Brush FieldBackground => CanEdit ? Brushes.White : Brushes.LightGray;
        public bool IsDateReadOnly => true;

        private void LoadData()
        {
            if (_isNewEquipment)
            {
                LoadNewEquipmentData();
            }
            else
            {
                LoadExistingEquipmentData();
            }
        }

        private void LoadNewEquipmentData()
        {
            EquipmentName = string.Empty;
            Description = string.Empty;
            InventoryNumber = GenerateInventoryNumber();
            DepartmentId = CanSelectDepartment ? null : _user.DepartmentID;
            RoomId = 0;
            Weight = 0;
            ServiceLifeYears = 1;
            RegistrationDate = DateTime.Now;
            PhotoPath = null;
            _originalPhotoPath = null;
            _oldPhotoPath = null;
        }

        private void LoadExistingEquipmentData()
        {
            EquipmentName = _equipment.Name;
            InventoryNumber = _equipment.InventoryNumber;
            Description = _equipment.Description;
            DepartmentId = _equipment.DepartmentId ?? _equipment.Room?.DepartmentId;
            RoomId = _equipment.RoomId ?? 0;
            Weight = _equipment.Weight;
            ServiceLifeYears = _equipment.ServiceLifeYears;
            RegistrationDate = _equipment.RegistrationDate;

            LoadEquipmentPhoto();
        }

        private void LoadEquipmentPhoto()
        {
            var photoFileName = _equipment.PhotoPath;

            if (string.IsNullOrEmpty(photoFileName))
            {
                PhotoPath = null;
                _originalPhotoPath = null;
                _oldPhotoPath = null;
                return;
            }

            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "Images", photoFileName);

            if (File.Exists(fullPath))
            {
                PhotoPath = fullPath;
            }
            else
            {
                PhotoPath = null;
            }

            _originalPhotoPath = photoFileName;
            _oldPhotoPath = photoFileName;
        }

        private string GenerateInventoryNumber()
        {
            const string prefix = "INV-";
            var datePart = DateTime.Now.ToString("yyyyMMdd");
            var random = new Random();

            string GenerateNumber()
            {
                var randomPart = random.Next(1000, 9999).ToString();
                return $"{prefix}{datePart}-{randomPart}";
            }

            var number = GenerateNumber();

            while (_context.Equipment.Any(e => e.InventoryNumber == number))
            {
                number = GenerateNumber();
            }

            return number;
        }

        private void LoadDepartments()
        {
            var query = _context.Departments.AsQueryable();

            if (!CanSelectDepartment)
            {
                query = query.Where(department => department.DepartmentId == _user.DepartmentID);
            }

            Departments = query
                .OrderBy(department => department.FullName)
                .ToList();
        }

        private void LoadRooms()
        {
            var roomsList = new List<Room> { CreateEmptyRoom() };

            if (DepartmentId.HasValue && DepartmentId.Value > 0)
            {
                var rooms = _context.Rooms
                    .Where(room => room.DepartmentId == DepartmentId.Value)
                    .OrderBy(room => room.RoomNumber)
                    .ToList();

                roomsList.AddRange(rooms);
            }

            Rooms = roomsList;

            if (!RoomId.HasValue || RoomId.Value == 0)
            {
                RoomId = 0;
            }
        }

        private static Room CreateEmptyRoom()
        {
            return new Room { RoomId = 0, RoomNumber = "Не выбрано" };
        }

        private void UpdatePhotoImage(string? photoPath)
        {
            if (!string.IsNullOrEmpty(photoPath) && File.Exists(photoPath))
            {
                PhotoImage = ImageHelper.LoadImage(photoPath);
            }
            else
            {
                PhotoImage = ImageHelper.LoadStubImage();
            }
        }

        /// <summary>
        /// Проверяет, соответствует ли изображение требуемым размерам
        /// </summary>
        private bool ValidateImageDimensions(string imagePath, out string errorMessage)
        {
            errorMessage = string.Empty;

            try
            {
                var fileInfo = new FileInfo(imagePath);
                if (fileInfo.Length > MAX_FILE_SIZE)
                {
                    errorMessage = $"Размер файла превышает {MAX_FILE_SIZE / 1024 / 1024} МБ. Пожалуйста, выберите файл меньшего размера.";
                    return false;
                }

                using (var fileStream = new FileStream(imagePath, FileMode.Open, FileAccess.Read))
                {
                    var decoder = BitmapDecoder.Create(fileStream, BitmapCreateOptions.IgnoreImageCache, BitmapCacheOption.None);
                    var frame = decoder.Frames[0];
                    
                    int width = frame.PixelWidth;
                    int height = frame.PixelHeight;

                    if (width != REQUIRED_WIDTH || height != REQUIRED_HEIGHT)
                    {
                        errorMessage = $"Фото должно быть размером {REQUIRED_WIDTH}x{REQUIRED_HEIGHT} пикселей. Текущий размер: {width}x{height} пикселей.";
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Ошибка при проверке изображения: {ex.Message}";
                return false;
            }
        }

        private void SelectPhotoButton_Click(object sender, RoutedEventArgs e)
        {
            if (!CanEdit)
            {
                return;
            }

            var openFileDialog = CreatePhotoFileDialog();

            if (openFileDialog.ShowDialog() == true)
            {
                if (!ValidateImageDimensions(openFileDialog.FileName, out string validationError))
                {
                    MessageBox.Show(validationError, "Ошибка валидации", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                ProcessSelectedPhoto(openFileDialog.FileName);
            }
        }

        private static OpenFileDialog CreatePhotoFileDialog()
        {
            return new OpenFileDialog
            {
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp",
                Title = "Выберите фото (требуемый размер: 300x200 px)"
            };
        }

        private void ProcessSelectedPhoto(string sourceFilePath)
        {
            var newFileName = ImageHelper.SaveImage(sourceFilePath);

            if (string.IsNullOrEmpty(newFileName))
            {
                return;
            }

            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "Images", newFileName);

            _originalPhotoPath = newFileName;
            PhotoPath = fullPath;
            _hasChanges = true;
        }

        private void DeletePhotoButton_Click(object sender, RoutedEventArgs e)
        {
            if (!CanEdit)
            {
                return;
            }

            var result = ShowConfirmationDialog("Удалить фото?", "Подтверждение");

            if (result == MessageBoxResult.Yes)
            {
                DeleteCurrentPhoto();
            }
        }

        private void DeleteCurrentPhoto()
        {
            if (!string.IsNullOrEmpty(_originalPhotoPath))
            {
                ImageHelper.DeleteImage(_originalPhotoPath);
            }

            _originalPhotoPath = null;
            PhotoPath = null;
            _hasChanges = true;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!CanEdit)
            {
                return;
            }

            if (!ValidateInput())
            {
                return;
            }

            try
            {
                UpdateEquipmentFromForm();
                SaveEquipmentToDatabase();
                ShowSuccessMessage();
                CloseWindow();
            }
            catch (DbUpdateException dbException)
            {
                HandleDatabaseException(dbException);
            }
            catch (Exception exception)
            {
                ShowErrorMessage("Ошибка при сохранении", exception);
            }
        }

        private bool ValidateInput()
        {
            if (string.IsNullOrWhiteSpace(EquipmentName))
            {
                MessageBox.Show("Введите наименование оборудования.");
                return false;
            }

            if (Weight < 0)
            {
                MessageBox.Show("Вес не может быть отрицательным.");
                return false;
            }

            if (ServiceLifeYears <= 0)
            {
                MessageBox.Show("Нормативный срок службы должен быть положительным.");
                return false;
            }

            if (!DepartmentId.HasValue)
            {
                MessageBox.Show("Выберите подразделение.");
                return false;
            }

            if (!string.IsNullOrEmpty(_originalPhotoPath))
            {
                var fullPhotoPath = Path.Combine(Directory.GetCurrentDirectory(), "Images", _originalPhotoPath);
                if (File.Exists(fullPhotoPath))
                {
                    if (!ValidateImageDimensions(fullPhotoPath, out string validationError))
                    {
                        MessageBox.Show($"Ошибка валидации фото: {validationError}", 
                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return false;
                    }
                }
            }

            return true;
        }

        private void UpdateEquipmentFromForm()
        {
            _equipment.Name = EquipmentName;
            _equipment.Description = Description;
            _equipment.Weight = Weight;
            _equipment.ServiceLifeYears = ServiceLifeYears;
            _equipment.RoomId = GetRoomIdOrNull();

            _equipment.DepartmentId = _equipment.RoomId.HasValue ? null : DepartmentId;

            if (_isNewEquipment)
            {
                InitializeNewEquipment();
            }
            else
            {
                UpdateExistingEquipmentPhoto();
            }
        }

        private int? GetRoomIdOrNull()
        {
            return RoomId.HasValue && RoomId.Value > 0 ? RoomId.Value : null;
        }

        private void InitializeNewEquipment()
        {
            _equipment.InventoryNumber = InventoryNumber;
            _equipment.RegistrationDate = RegistrationDate;
            _equipment.PhotoPath = _originalPhotoPath;
            _equipment.IsArchived = false;
            _equipment.LocationType = "R"; // По умолчанию

            _context.Equipment.Add(_equipment);
        }

        private void UpdateExistingEquipmentPhoto()
        {
            if (_oldPhotoPath != _originalPhotoPath)
            {
                if (!string.IsNullOrEmpty(_oldPhotoPath))
                {
                    ImageHelper.DeleteImage(_oldPhotoPath);
                }
            }

            _equipment.PhotoPath = _originalPhotoPath;
        }

        private void SaveEquipmentToDatabase()
        {
            _context.SaveChanges();
        }

        private void ShowSuccessMessage()
        {
            MessageBox.Show("Данные успешно сохранены.", "Успех",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void HandleDatabaseException(DbUpdateException dbException)
        {
            var errorMessage = ParseDatabaseException(dbException);
            MessageBox.Show(errorMessage, "Ошибка базы данных",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private string ParseDatabaseException(DbUpdateException dbException)
        {
            if (dbException.InnerException == null)
            {
                return $"Ошибка при сохранении в базу данных:\n{dbException.Message}";
            }

            var innerMessage = dbException.InnerException.Message;

            if (innerMessage.Contains("FK__Equipment__Depar"))
            {
                return "Ошибка внешнего ключа: Указанное подразделение не существует.";
            }

            if (innerMessage.Contains("FK__Equipment__RoomI"))
            {
                return "Ошибка внешнего ключа: Указанная аудитория не существует.";
            }

            if (innerMessage.Contains("UQ__Equipmen__D6D65CC8"))
            {
                return "Оборудование с таким инвентарным номером уже существует.";
            }

            return $"Ошибка при сохранении в базу данных:\n{innerMessage}";
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (!CanDelete || _isNewEquipment)
            {
                return;
            }

            var result = ShowConfirmationDialog("Удалить оборудование?", "Подтверждение");

            if (result == MessageBoxResult.Yes)
            {
                DeleteEquipment();
            }
        }

        private void DeleteEquipment()
        {
            try
            {
                _context.Equipment.Remove(_equipment);
                _context.SaveChanges();

                if (!string.IsNullOrEmpty(_oldPhotoPath))
                {
                    ImageHelper.DeleteImage(_oldPhotoPath);
                }

                CloseWindow();
            }
            catch (Exception exception)
            {
                ShowErrorMessage("Ошибка при удалении", exception);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_hasChanges)
            {
                var result = ShowConfirmationDialog(
                    "Есть несохраненные изменения. Закрыть без сохранения?",
                    "Подтверждение");

                if (result == MessageBoxResult.No)
                {
                    return;
                }
            }

            CloseWindow();
        }

        private void CloseWindow()
        {
            _hasChanges = false;
            Close();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
        }

        private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return;
            }

            field = value;
            OnPropertyChanged(propertyName);
            _hasChanges = true;
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private static MessageBoxResult ShowConfirmationDialog(string message, string caption)
        {
            return MessageBox.Show(message, caption,
                MessageBoxButton.YesNo, MessageBoxImage.Question);
        }

        private static void ShowErrorMessage(string message, Exception exception)
        {
            MessageBox.Show($"{message}: {exception.Message}",
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
