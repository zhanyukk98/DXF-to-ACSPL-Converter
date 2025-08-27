using DXFtoACSPL.WinForms.Forms;
using System.Text;

namespace DXFtoACSPL.WinForms;

static class Program
{
    /// <summary>
    /// 应用程序的主入口点
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {
        // 注册编码提供程序，支持 GB2312 等编码
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        
        // 启用视觉样式
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // 设置异常处理
        Application.ThreadException += Application_ThreadException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

        // 启动主窗体
        var startupPath = (args != null && args.Length > 0) ? args[0] : null;
        Application.Run(new MainForm(startupPath));
    }

    private static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
    {
        MessageBox.Show($"发生未处理的线程异常:\n{e.Exception.Message}", "错误",
            MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            MessageBox.Show($"发生未处理的应用程序异常:\n{ex.Message}", "严重错误",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}