namespace HPParking.Api.Configuration
{
    public class StorageSettings
    {
        public string RootPath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "hpparking");
        public string RequestPath { get; set; } = "/images";
        public StorageFolders Folders { get; set; } = new();
        public long MaxSizeBytes { get; set; } = 5242880; // 5MB
        public List<string> AllowedExtensions { get; set; } = [".jpg", ".jpeg", ".png", ".webp"];

        /// <summary>
        /// Đường dẫn vật lý thư mục lưu Avatar (tương thích ngược với các service cũ)
        /// </summary>
        public string UploadPath => Path.Combine(RootPath, Folders.Avatar);
    }

    public class StorageFolders
    {
        public string Avatar { get; set; } = "Avatar";
        public string Captures { get; set; } = "Captures";
        public string Documents { get; set; } = "Documents";
        public string Reports { get; set; } = "Reports";
    }
}
