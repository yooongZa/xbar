using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using XBar.App;
using XBar.Core;

namespace XBar.WindowsTests;

internal static class CalendarTests
{
    public static async Task Run(Action<string, bool, string?> check)
    {
        using var session = new WebSession(Path.Combine(Path.GetTempPath(), "XBar-calendar-" + Guid.NewGuid()));
        var surface = await session.CreateAsync(true, "X-Bar calendar fixture");
        string body = "";
        var core = surface.View.CoreWebView2;
        core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
        core.WebResourceRequested += (_, e) => e.Response = core.Environment.CreateWebResourceResponse(
            new MemoryStream(Encoding.UTF8.GetBytes("<!doctype html><meta charset='utf-8'><meta http-equiv='Content-Security-Policy' content=\"default-src 'none'; script-src 'unsafe-inline'; style-src 'unsafe-inline'\">" + body)), 200, "OK", "Content-Type: text/html; charset=utf-8");
        Task<PageReading?> Read(bool advance = false) => surface.ReadAsync<PageReading>("TodayBarExtractor." + (advance ? "advance" : "snapshot") + "()", CancellationToken.None);
        var fixture = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "date-range.html"));
        foreach (var (locale, label) in new[] { ("ko-KR", "기간 선택"), ("en-US", "Choose a period"), ("ja-JP", "表示する期間") }) {
            body = fixture.Replace("__LOCALE__", locale).Replace("__LABEL__", label);
            await surface.NavigateAsync("https://x.com/i/account_analytics", CancellationToken.None);
            check("Icon calendar requires live date evidence: " + locale, await Read() is { Status: "wrong_day", Display: null }, null);
            int samples = 0; PageReading? reading = null;
            for (int i = 0; i < 18; i++) {
                reading = await Read(true);
                if (reading?.Status == "ready") { if (++samples == 2) break; } else samples = 0;
                await Task.Delay(1000);
            }
            check("Icon calendar verifies dates and settled metric: " + locale, samples == 2 && reading is { Display: "42" }, JsonSerializer.Serialize(reading));
            check("Closed calendar cannot reuse cached range: " + locale, await Read() is { Status: "wrong_day" }, null);
            await core.ExecuteScriptAsync("document.getElementById('date').click();calendarFixture.setRange(-6,0)");
            check("Changed range rejects old today evidence: " + locale, await Read() is { Status: "wrong_day", Display: null }, null);
            await core.ExecuteScriptAsync("calendarFixture.setRange(0,0);document.querySelector('[aria-selected=true] button').setAttribute('aria-label','2020년 10월 30일 금요일')");
            check("Endpoint dates require full-year grid evidence: " + locale, await Read() is { Status: "wrong_day" }, null);
            await core.ExecuteScriptAsync("calendarFixture.setRange(0,0);document.getElementById('date').setAttribute('aria-label','Changed label');document.querySelector('#date svg').remove()");
            check("Linked calendar survives label and icon changes: " + locale, await Read() is { Status: "ready" }, null);
            await core.ExecuteScriptAsync("const now=new Date();document.getElementById('date').textContent=`${now.getFullYear()}. ${now.getMonth()+1}. ${now.getDate()}.`;calendarFixture.setRange(-6,0)");
            check("Live calendar overrides stale date label: " + locale, await Read() is { Status: "wrong_day" }, null);
            await core.ExecuteScriptAsync("const duplicate=document.getElementById('date').cloneNode(true);duplicate.id='duplicate';document.body.append(duplicate)");
            check("Multiple calendar controls are rejected: " + locale, await Read(true) is { Action: "date_control_ambiguous", Display: null }, null);
        }
    }
}
