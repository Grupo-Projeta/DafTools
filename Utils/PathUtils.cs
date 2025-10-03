using Microsoft.WindowsAPICodePack.Dialogs;

namespace DafTools.Utils
{
    public class PathUtils
    {
        public string GetPathByInput()
        {
            var dialog = new CommonOpenFileDialog { IsFolderPicker = true };

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                string selectedPath = dialog.FileName ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(selectedPath))
                {
                    Console.WriteLine();
                    Console.WriteLine("Pasta Escolhida: " + selectedPath);
                    Console.WriteLine();
                    Console.WriteLine("-------------------------------------");

                    return selectedPath;
                }
            }

            Console.WriteLine();
            Console.WriteLine("Não foi definido um caminho para gerar o arquivo");
            return string.Empty;
        }

    }
}
