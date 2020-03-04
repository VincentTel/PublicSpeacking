using UnityEngine;
using SDD.Events;
using System.IO;
using SFB;
using UnityEngine.UI;

public class DirectoryManager : MonoBehaviour
{
    private string userRoot = null;
    private string slideRoot = null;
    public int idLength = 8;
    public Text idText = null;

    public float _buttonCooldown = 3;

    private string path = null;
    private string slidePath = null;

    private bool isPlaying;
    private bool isRecording;

    private float timeTriggered;

    void Awake()
    {
        userRoot = Path.Combine(Application.persistentDataPath, "TrainingData", "Users");
        slideRoot = Path.Combine(Application.persistentDataPath, "TrainingData", "Demo", "Slides");
        if (Application.systemLanguage == SystemLanguage.Japanese)
        {
            path = Path.Combine(Application.persistentDataPath, "TrainingData", "Demo", "JP");
        }
        else
        {
            path = Path.Combine(Application.persistentDataPath, "TrainingData", "Demo", "EN");
        }

        slidePath = path;

        isPlaying = false;
        isRecording = false;
        SubscribeEvents();

        // Create default app directory if does not exist, means that we need to copy demoFile to the persistent file
        if (!TestFolderExistance(userRoot))
        {
            Directory.CreateDirectory(userRoot);
        }

        // path not existing = slides not existing: copy the demo to the default paths
        if (!TestFolderExistance(path))
        {
            Directory.CreateDirectory(path);
            InitDemo();
        }

        timeTriggered = Time.time;
    }

    private void InitDemo()
    {
        string demoResourcePath = Path.Combine(Application.streamingAssetsPath, "Demo");
        if (TestFolderExistance(demoResourcePath))
        {
            string demoPath = Path.Combine(Application.persistentDataPath, "TrainingData", "Demo");
            DirectoryCopy(demoResourcePath, demoPath, true);
        }
    }

    private void SubscribeEvents()
    {
        EventManager.Instance.AddListener<EndViewModeEvent>(EndViewModelEventHandler);
    }

    private void EndViewModelEventHandler(EndViewModeEvent e)
    {
        isPlaying = false;
    }

    private void CancelEvents()
    {
        EventManager.Instance.RemoveListener<EndViewModeEvent>(EndViewModelEventHandler);
    }

    void OnDestroy()
    {
        CancelEvents();
    }

    // Start is called before the first frame update
    void Start()
    {
        // Make sure to have a / at the end of the user's root directory
        if (!userRoot.ToLower().EndsWith("\\"))
        {
            userRoot += "\\";
        }

        //By default, load the demo
        SetSlides();
        SetPath();
    }

    private void SetPath()
    {
        string id = Path.GetFileName(path);
        SetIDText(id);

        TestPathEnd();

        Debug.Log("Selected path : " + path);

        EventManager.Instance.Raise(new SetPathEvents()
        {
            Path = path
        });
    }

    //Use for new button
    public void CreateNewRecord()
    {
        if (isPlaying || isRecording)
        {
            return;
        }

        //Generate an ID
        string id = GenerateID(idLength);

        //Test if it already exist -> If so regenerate
        while (TestIDExistance(id))
        {
            id = GenerateID(idLength);
        }

        path = userRoot + id;
        //Create the folder
        Directory.CreateDirectory(path);

        slidePath = slideRoot;

        SetSlides();
        //Set the path
        SetPath();
    }

    public void LoadRecord()
    {
        if (isPlaying || isRecording)
        {
            return;
        }

        //Search for the folder
        string folder = GetFolder("Load reccord");

        //If wrong path, return
        if (!TestFolderExistance(folder))
        {
            return;
        }

        path = folder;
        slidePath = folder;

        SetSlides();
        //Set the path
        SetPath();
    }

    public void ChangePlayingStatement()
    {
        if (timeTriggered > Time.time)
        {
            return;
        }
        timeTriggered += _buttonCooldown;

        isPlaying = !isPlaying;
    }

    public void ChangeRecordingStatement()
    {
        if (timeTriggered > Time.time)
        {
            return;
        }
        timeTriggered += _buttonCooldown;

        isRecording = !isRecording;
    }

    public void LoadSlides()
    {
        if (isPlaying || isRecording)
        {
            return;
        }

        SetSlidePath();
        SetSlides(false);
    }

    private string GetFolder(string message)
    {
        //use https://github.com/gkngkc/UnityStandaloneFileBrowser
        string[] path = StandaloneFileBrowser.OpenFolderPanel(message, "", false);

        if (path.Length == 0)
        {
            return "";
        }

        return path[0];
    }

    private bool TestFolderExistance(string folderPath)
    {
        return Directory.Exists(folderPath);
    }

    private void TestPathEnd()
    {
        // Make sure to have a / at the end
        if (!path.ToLower().EndsWith("\\"))
        {
            path += "\\";
        }
    }

    private string GenerateID(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        string id = "";
        for (int i = 0; i < length; i++)
        {
            id += chars[Random.Range(0, chars.Length)];
        }

        return id;
    }

    private void SetIDText(string userId)
    {
        if (idText == null)
        {
            return;
        }

        idText.text = userId;
    }

    // True if already exist
    // False if new
    private bool TestIDExistance(string id)
    {
        if (File.Exists(userRoot + id))
        {
            return true;
        }

        return false;
    }

    private void CopySlidesCurrentUser()
    {
        string[] images = Directory.GetFiles(slidePath, "*.png");
        foreach (string name in images)
        {
            File.Copy(name, Path.Combine(path, Path.GetFileName(name)));
        }
    }

    private void RemoveSlidesCurrentUser()
    {
        string[] images = Directory.GetFiles(path, "*.png");
        foreach (string name in images)
        {
            File.Delete(name);
        }
    }

    private Texture2D[] LoadSlidesTextures()
    {
        string[] images = Directory.GetFiles(slidePath, "*.png");
        if (images.Length <= 0)
        {
            return null;
        }

        Texture2D[] textures = new Texture2D[images.Length];
        int cpt = 0;

        foreach (string name in images)
        {
            byte[] fileData = File.ReadAllBytes(name);
            textures[cpt] = new Texture2D(2, 2);
            textures[cpt].LoadImage(fileData);
            cpt++;
        }

        return textures;
    }

    private bool CheckSlideAvailability()
    {
        //If wrong path, return
        if (!TestFolderExistance(slidePath))
        {
            return false;
        }

        string[] images = Directory.GetFiles(slidePath, "*.png");
        if (images.Length > 0)
        {
            return true;
        }

        return false;
    }

    private void SetSlidePath()
    {
        slidePath = GetFolder("Get Slides Path");
    }

    private void SetSlides(bool check = true)
    {
        if (!CheckSlideAvailability() && !check)
        {
            return;
        }

        if (!CheckSlideAvailability())
        {
            SetSlidePath();
        }

        if (slidePath != path)
        {
            RemoveSlidesCurrentUser();
            CopySlidesCurrentUser();
        }

        Texture2D[] slideTextures = LoadSlidesTextures();

        if (slideTextures.Length > 0)
        {
            EventManager.Instance.Raise(new SetSlidesTextureEvents()
            {
                textures = slideTextures
            });
        }
    }

    // From https://docs.microsoft.com/en-us/dotnet/standard/io/how-to-copy-directories?redirectedfrom=MSDN
    // Modify to not copy meta file
    private void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
    {
        // Get the subdirectories for the specified directory.
        DirectoryInfo dir = new DirectoryInfo(sourceDirName);

        if (!dir.Exists)
        {
            throw new DirectoryNotFoundException(
                "Source directory does not exist or could not be found: "
                + sourceDirName);
        }

        DirectoryInfo[] dirs = dir.GetDirectories();
        // If the destination directory doesn't exist, create it.
        if (!Directory.Exists(destDirName))
        {
            Directory.CreateDirectory(destDirName);
        }

        // Get the files in the directory and copy them to the new location.
        FileInfo[] files = dir.GetFiles();
        foreach (FileInfo file in files)
        {
            //Do not copy unity meta files
            if (file.Name.Contains("meta"))
            {
                continue;
            }
            string temppath = Path.Combine(destDirName, file.Name);
            file.CopyTo(temppath, false);
        }

        // If copying subdirectories, copy them and their contents to new location.
        if (copySubDirs)
        {
            foreach (DirectoryInfo subdir in dirs)
            {
                string temppath = Path.Combine(destDirName, subdir.Name);
                DirectoryCopy(subdir.FullName, temppath, copySubDirs);
            }
        }
    }
}
