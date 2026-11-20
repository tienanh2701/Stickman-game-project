namespace StickmanBeatEmUp.CorePure.Interfaces
{
    /// <summary>
    /// Hợp đồng trừu tượng lưu/tải chuỗi dữ liệu bền vững (DIP), hỗ trợ thay File JSON bằng InMemory Fake trong Unit Test.
    /// </summary>
    public interface ISaveStorage
    {
        void SaveText(string key, string content);

        string LoadText(string key);

        bool Exists(string key);

        void Delete(string key);
    }
}
