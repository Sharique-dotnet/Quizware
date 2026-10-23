using System.Net;
using System.Text;
using ClosedXML.Excel;
using FluentAssertions;
using Quizware.Application.Authorization;

namespace Quizware.Api.IntegrationTests;

/// <summary>P13-06: every report exports to Excel, CSV and PDF with the same values.</summary>
public class ReportExportTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ReportExportTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private sealed record Played(QualificationTestHarness Q, Guid StageId, Guid MatchId, IReadOnlyList<Guid> Teams)
    {
        public string ExportUrl => $"/api/v1/programs/{Q.Match.ProgramId}/reports/export";
    }

    private async Task<Played> PlayedMatchAsync()
    {
        var q = await QualificationTestHarness.CreateAsync(_factory);
        var stage = await q.CreateStageAsync(4);
        var t = await q.Match.CreateTeamsAsync(2);
        var match = await q.PlayMatchAsync(stage.Id, (t[0], "CC"), (t[1], "CI"));
        return new Played(q, stage.Id, match.Id, t);
    }

    private static async Task<byte[]> DownloadAsync(HttpClient client, string url, string contentType, string extension)
    {
        var response = await client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        response.Content.Headers.ContentType!.MediaType.Should().Be(contentType);
        response.Content.Headers.ContentDisposition!.FileName.Should().EndWith($".{extension}");
        return await response.Content.ReadAsByteArrayAsync();
    }

    [Fact]
    public async Task MatchReport_ExportsToExcel_WithASheetPerSection_AndNumbersAsNumbers()
    {
        var p = await PlayedMatchAsync();

        var bytes = await DownloadAsync(
            p.Q.Client, $"{p.ExportUrl}/match?id={p.MatchId}&format=xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "xlsx");

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        workbook.Worksheets.Select(w => w.Name).Should().Equal("Summary", "Teams", "Questions", "Timeline");
        var teams = workbook.Worksheet("Teams");
        teams.Cell(1, 2).GetString().Should().Be("Team");
        teams.Cell(2, 1).GetDouble().Should().Be(1);
        teams.Cell(2, 5).GetDouble().Should().Be(20);
        workbook.Worksheet("Questions").RowsUsed().Count().Should().Be(5, "a header row and the 4 questions served");
        workbook.Worksheet("Timeline").RowsUsed().Count().Should().BeGreaterThan(5);
    }

    [Fact]
    public async Task Reports_ExportToCsv_WithEachSectionUnderItsName()
    {
        var p = await PlayedMatchAsync();

        var stage = Encoding.UTF8.GetString(await DownloadAsync(
            p.Q.Client, $"{p.ExportUrl}/stage?id={p.StageId}&format=csv", "text/csv", "csv"));
        var team = Encoding.UTF8.GetString(await DownloadAsync(
            p.Q.Client, $"{p.ExportUrl}/team?id={p.Teams[0]}&format=csv", "text/csv", "csv"));
        var usage = Encoding.UTF8.GetString(await DownloadAsync(
            p.Q.Client, $"{p.ExportUrl}/question-usage?format=csv", "text/csv", "csv"));

        stage.Should().StartWith("﻿Stage summary").And.Contain("\r\nStandings\r\nRank,Team,Score\r\n1,");
        stage.Should().Contain("\r\nMatches\r\nMatch,Kind,State,Winner,Tied\r\n");
        team.Should().Contain("Matches played,1").And.Contain("Total score,20").And.Contain("Accuracy,100%");
        usage.Should().Contain("Question,Format,Difficulty,Times used").And.Contain("EveryoneCorrect");
    }

    [Fact]
    public async Task Reports_ExportToPdf_AsAValidDocument()
    {
        var p = await PlayedMatchAsync();
        var auditor = await p.Q.Match.CreateClientAsync(Roles.Auditor);

        var match = await DownloadAsync(p.Q.Client, $"{p.ExportUrl}/match?id={p.MatchId}&format=pdf", "application/pdf", "pdf");
        var audit = await DownloadAsync(auditor, $"{p.ExportUrl}/audit?format=pdf&entityName=Team", "application/pdf", "pdf");

        foreach (var pdf in new[] { match, audit })
        {
            var text = Encoding.Latin1.GetString(pdf);
            text.Should().StartWith("%PDF-1.4").And.EndWith("%%EOF\n");
            var startXref = long.Parse(text[(text.LastIndexOf("startxref\n", StringComparison.Ordinal) + 10)..].Split('\n')[0]);
            text.Substring((int)startXref, 4).Should().Be("xref", "the cross-reference offset must point at the table");
        }

        Encoding.Latin1.GetString(match).Should().Contain("/Title <FEFF", "document info strings are UTF-16, not WinAnsi");
        Encoding.Latin1.GetString(match).Should().Contain("(Teams)").And.Contain("Timeline").And.Contain("MatchStarted");
        Encoding.Latin1.GetString(audit).Should().Contain("Audit log").And.Contain("Insert");
    }

    [Fact]
    public async Task Export_RejectsUnknownTypesAndFormats_AndAMissingId()
    {
        var p = await PlayedMatchAsync();
        var operatorClient = await p.Q.Match.CreateClientAsync(Roles.Operator);

        var unknownType = await p.Q.Client.GetAsync($"{p.ExportUrl}/scores?format=csv");
        var unknownFormat = await p.Q.Client.GetAsync($"{p.ExportUrl}/match?id={p.MatchId}&format=docx");
        var missingId = await p.Q.Client.GetAsync($"{p.ExportUrl}/match?format=csv");
        var otherMatch = await p.Q.Client.GetAsync($"{p.ExportUrl}/match?id={Guid.NewGuid()}&format=csv");
        var asOperator = await operatorClient.GetAsync($"{p.ExportUrl}/match?id={p.MatchId}&format=csv");

        unknownType.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        unknownFormat.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        missingId.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        otherMatch.StatusCode.Should().Be(HttpStatusCode.NotFound);
        asOperator.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
