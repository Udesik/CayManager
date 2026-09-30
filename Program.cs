using System;
using System.IO;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Threading;

class Program
{
    static void Main(string[] args)
    {
        IOMaster ioMaster = new IOMaster();

        ViewCells viewCells = new ViewCells(ioMaster.GetComposition());
        viewCells.Start();
    }
}

public class ViewCells
{
    private const string CommandShow = "1";
    private const string CommandAdd = "2";
    private const string CommandDelete = "3";
    private const string CommandExit = "exit";

    private bool _isWorking = true;

    private Composition _composition;

    public ViewCells(Composition composition)
    {
        _composition = composition;
    }

    public void Start()
    {
        while (_isWorking)
        {
            Console.Clear();
            Console.WriteLine("Действия с базой данных: ");
            Console.WriteLine($"{CommandShow} - Вывести все пароли");
            Console.WriteLine($"{CommandAdd} - Добавить пароль");
            Console.WriteLine($"{CommandDelete} - Удалить пароль");
            Console.WriteLine($"{CommandExit} - Выход");

            string command = Console.ReadLine();

            switch (command)
            {
                case CommandShow:
                    Console.Clear();
                    _composition.ViewCells();
                    break;

                case CommandAdd:
                    _composition.AddCell();
                    break;

                case CommandDelete:
                    _composition.DeleteCell();
                    break;

                case CommandExit:
                    _isWorking = false;
                    break;

                default:
                    Console.WriteLine("Неверная команда.");
                    break;
            }

            Thread.Sleep(3000);
        }
    }
}

public class IOMaster
{
    private string _masterPassword = "Ваш_Секретный_Пароль_123";
    private byte[] _salt = new byte[] { 42, 12, 99, 201, 85, 66, 14, 73 };

    private string _filePath = "secure_vault.dat";
    private byte[] _key;

    private Composition _composition;

    public IOMaster()
    {
        using (var deriveBytes = new Rfc2898DeriveBytes(_masterPassword, _salt, 100_000, HashAlgorithmName.SHA256))
        {
            _key = deriveBytes.GetBytes(32);
        }

        if (!File.Exists(_filePath))
        {
            _composition = new Composition(new List<Cell>());

            CompositionStorage.SaveToFile(_composition, _filePath, _key);
        }
        else
        {
            try
            {
                _composition = CompositionStorage.LoadFromFile(_filePath, _key);
            }
            catch (CryptographicException)
            {
                Console.WriteLine("Ошибка: неверный ключ шифрования или файл был изменен.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Произошла ошибка: {ex.Message}");
            }
        }

        _composition.SaveMe += () => CompositionStorage.SaveToFile(_composition, _filePath, _key);
    }

    public Composition GetComposition()
    {
        return _composition;
    }
}

public class Composition
{
    [JsonProperty]
    private List<Cell> _cells;

    public event Action SaveMe;

    public Composition(List<Cell> cells)
    {
        _cells = cells;
    }

    public void AddCell()
    {
        string citeLink;
        string nickname;
        string password;

        Console.Write("Введите ссылку на сайт: ");
        citeLink = Console.ReadLine();

        if (string.IsNullOrEmpty(citeLink))
        {
            Console.WriteLine("Неверный ввод.");
            return;
        }

        Console.Write("Введите логин: ");
        nickname = Console.ReadLine();

        if (string.IsNullOrEmpty(nickname))
        {
            Console.WriteLine("Неверный ввод.");
            return;
        }

        Console.Write("Введите пароль: ");
        password = Console.ReadLine();

        if (string.IsNullOrEmpty(password))
        {
            Console.WriteLine("Неверный ввод.");
            return;
        }

        Console.WriteLine("Вы точно уверены? (y/n)");
        string confirmation = Console.ReadLine();

        if (confirmation == "n")
        {
            return;
        }
        else if (confirmation != "y")
        {
            Console.WriteLine("Неверный ввод.");
            return;
        }

        Cell newCell = new Cell(citeLink, nickname, password);
        _cells.Add(newCell);

        SaveMe?.Invoke();
    }

    public void DeleteCell()
    {
        Console.Clear();
        ViewCells();

        Console.Write("Введите номер ячейки для удаления: ");

        if (int.TryParse(Console.ReadLine(), out int index))
        {
            if (index >= 1 && index <= _cells.Count)
            {
                _cells.RemoveAt(index - 1);
                SaveMe?.Invoke();
            }
            else
            {
                Console.WriteLine("Неверно введён номер ячейки");
                return;
            }
        }
        else
        {
            Console.WriteLine("Неверный ввод.");
            return;
        }
    }

    public void ViewCells()
    {
        if (_cells.Count == 0)
        {
            Console.WriteLine("Список пуст.");
        }
        else
        {
            foreach (Cell cell in _cells)
            {
                Console.WriteLine($"{_cells.IndexOf(cell) + 1}. Сайт: {cell.CiteLink} | Логин: {cell.Nickname} | Пароль: {cell.Password}");
            }
        }
    }
}

public struct Cell
{
    public Cell(string citeLink, string nickname, string password)
    {
        CiteLink = citeLink;
        Nickname = nickname;
        Password = password;
    }

    public string CiteLink { get; private set; }
    public string Nickname { get; private set; }
    public string Password { get; private set; }
}

public static class CompositionStorage
{
    private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings();

    public static void SaveToFile(Composition composition, string filePath, byte[] key)
    {
        if (key.Length != 32)
        {
            throw new ArgumentException("Ключ должен быть длиной 32 байта.");
        }

        using (Aes aes = Aes.Create())
        {
            aes.Key = key;
            aes.GenerateIV();

            using (FileStream fsOutput = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                fsOutput.Write(aes.IV, 0, aes.IV.Length);

                using (ICryptoTransform encryptor = aes.CreateEncryptor())
                using (CryptoStream cs = new CryptoStream(fsOutput, encryptor, CryptoStreamMode.Write))
                using (StreamWriter writer = new StreamWriter(cs, Encoding.UTF8))
                {
                    string json = JsonConvert.SerializeObject(composition, Settings);
                    writer.Write(json);
                }
            }
        }
    }

    public static Composition LoadFromFile(string filePath, byte[] key)
    {
        if (key.Length != 32)
        {
            throw new ArgumentException("Ключ должен быть длиной 32 байта.");
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Файл не найден.");
        }

        using (Aes aes = Aes.Create())
        {
            aes.Key = key;

            using (FileStream fsInput = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                byte[] iv = new byte[aes.BlockSize / 8];
                fsInput.ReadExactly(iv, 0, iv.Length);
                aes.IV = iv;

                using (ICryptoTransform decryptor = aes.CreateDecryptor())
                using (CryptoStream cs = new CryptoStream(fsInput, decryptor, CryptoStreamMode.Read))
                using (StreamReader reader = new StreamReader(cs, Encoding.UTF8))
                {
                    string json = reader.ReadToEnd();

                    Composition? result = JsonConvert.DeserializeObject<Composition>(json, Settings);

                    return result ?? throw new InvalidDataException("Не удалось восстановить объект.");
                }
            }
        }
    }
}
