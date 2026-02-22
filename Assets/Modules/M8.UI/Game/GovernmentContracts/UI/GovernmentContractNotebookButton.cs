using UnityEngine;

namespace WrightTycoon.Game.GovernmentContracts;

public sealed class GovernmentContractNotebookButton : MonoBehaviour
{
    [SerializeField] private GovernmentContractNotebookWindow? _notebookWindow;

    // Hook this method into a UI button OnClick to reopen the notebook.
    public void ReopenNotebook()
    {
        if (_notebookWindow is null)
        {
            return;
        }

        _notebookWindow.Open();
    }
}
