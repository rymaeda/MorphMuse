using CamBam.UI;
using System;
using System.Windows.Forms;

public static class Program
{
    public static CamBamUI _ui;

    public static void InitPlugin(CamBamUI ui)
    {
        _ui = ui;
        ToolStripMenuItem mi = new ToolStripMenuItem("MorphMuse");
        mi.Click += MorphMuse_Click;
        ui.Menus.mnuPlugins.DropDownItems.Add(mi);
    }

    static void MorphMuse_Click(object sender, EventArgs e)
    {
        var controller = new MorphMuseController(_ui);
        controller.Execute();
    }
}