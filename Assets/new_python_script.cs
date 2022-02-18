using UnityEditor;
using UnityEditor.Scripting.Python;

public class MenuItem_Pythonexe_Class
{
   [MenuItem("Python Scripts/Pythonexe")]
   public static void Pythonexe()
   {
       PythonRunner.RunFile("Assets/new_python_script.py");
       }
};
