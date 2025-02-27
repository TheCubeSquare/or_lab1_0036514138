﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using TabletopGames.Models;
using TabletopGames.ViewModels;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;
using System.Linq;
using TabletopGames.Extensions.Selectors;
using System;
using System.Collections.Generic;
using System.Text;
using System.Web;
using Newtonsoft.Json;
using System.IO;

namespace TabletopGames.Controllers
{
    public class TabletopGamesController : Controller
    {
        private readonly TabletopGamesContext ctx;
        private readonly AppSettings appSettings;

        public TabletopGamesController(TabletopGamesContext ctx, IOptionsSnapshot<AppSettings> options)
        {
            this.ctx = ctx;
            appSettings = options.Value;
        }

        public async Task<IActionResult> Index(int page = 1, int sort = 1, bool ascending = true)
        {
            ViewBag.Tables = Output.Tables;

            var query = ctx.TabletopGame.AsNoTracking();

            query = query.ApplySort(sort, ascending);

            var tabletopGames = await query
                .Select(p => new TabletopGameViewModel
                {
                    IdGame = p.IdGame,
                    NameGame = p.NameGame,
                    YearGame = p.YearGame,
                    MinPlayers = p.MinPlayers,
                    MaxPlayers = p.MaxPlayers,
                    AverageRating = p.AverageRating,
                    AverageComplexity = p.AverageComplexity,
                    PlayTime = p.PlayTime
                })
                .ToListAsync();

            var pagingInfo = new PagingInfo
            {
                CurrentPage = page,
                Ascending = ascending,
                Sort = sort
            };

            var model = new TabletopGamesViewModel
            {
                TabletopGames = tabletopGames,
                PagingInfo = pagingInfo
            };

            return View(model);
        }

        public async Task<IActionResult> Details(int id)
        {
            ViewBag.Tables = Output.Tables;

            var tabletopGame = await ctx.TabletopGame
                                  .Select(p => new TabletopGameViewModel
                                  {
                                      IdGame = p.IdGame,
                                      NameGame = p.NameGame,
                                      YearGame = p.YearGame,
                                      MinPlayers = p.MinPlayers,
                                      MaxPlayers = p.MaxPlayers,
                                      AverageRating = p.AverageRating,
                                      AverageComplexity = p.AverageComplexity,
                                      PlayTime = p.PlayTime
                                  })
                                  .AsNoTracking()
                                  .Where(o => o.IdGame == id)
                                  .SingleOrDefaultAsync();

            if (tabletopGame != null)
            {
                var pagingInfo = new PagingInfo
                {
                    CurrentPage = 1,
                    Ascending = true,
                    Sort = 1
                };

                var model = new TabletopGameDetails
                {
                    TabletopGame = tabletopGame,
                    PagingInfo = pagingInfo
                };

                return View(model);
            }
            else
            {
                return NotFound($"Incorrect use of the id {id}");
            }
        }

        [HttpGet]
        public async Task<IActionResult> Create(int page = 1, int sort = 1, bool ascending = true)
        {
            ViewBag.Tables = Output.Tables;
            ViewBag.CurrentPage = page;
            ViewBag.Sort = sort;
            ViewBag.Ascending = ascending;
            await PrepareAdditionalValues();
            return View();
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TabletopGame tabletopGame, int page = 1, int sort = 1, bool ascending = true)
        {
            ViewBag.Tables = Output.Tables;

            if (ModelState.IsValid)
            {
                try
                {
                    ctx.Add(tabletopGame);
                    await ctx.SaveChangesAsync();
                    string name = char.ToUpper(tabletopGame.NameGame[0]) + tabletopGame.NameGame.Substring(1);
                    TempData[Output.Message] = $"{name} tabletop game added. Game ID = {tabletopGame.IdGame}";
                    TempData[Output.ErrorOccurred] = false;
                    return RedirectToAction(nameof(Index), new { page = page, sort = sort, ascending = ascending });
                }
                catch (Exception exc)
                {
                    ModelState.AddModelError(string.Empty, exc.Message);
                    await PrepareAdditionalValues();
                    return RedirectToAction(nameof(Create));
                }
            }
            else
            {
                await PrepareAdditionalValues();
                return RedirectToAction(nameof(Create));
            }
        }


        [HttpGet]
        public async Task<IActionResult> Edit(int id, int sort = 1, bool ascending = true)
        {
            ViewBag.Tables = Output.Tables;

            var tabletopGame = await ctx.TabletopGame
                                  .AsNoTracking()
                                  .Where(o => o.IdGame == id)
                                  .SingleOrDefaultAsync();
            if (tabletopGame != null)
            {
                ViewBag.Sort = sort;
                ViewBag.Ascending = ascending;
                await PrepareAdditionalValues();
                return View(tabletopGame);
            }
            else
            {
                return NotFound($"Incorrect tabletop game ID: {id}");
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(TabletopGame tabletopGame, int page = 1, int sort = 1, bool ascending = true)
        {
            ViewBag.Tables = Output.Tables;

            if (tabletopGame == null)
            {
                return NotFound("No data has been sent");
            }

            bool checkId = await ctx.TabletopGame.AnyAsync(p => p.IdGame == tabletopGame.IdGame);
            if (!checkId)
            {
                return NotFound($"Incorrect tabletop game ID: {tabletopGame?.IdGame}");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    ctx.Update(tabletopGame);
                    await ctx.SaveChangesAsync();
                    string name = char.ToUpper(tabletopGame.NameGame[0]) + tabletopGame.NameGame.Substring(1);
                    TempData[Output.Message] = $"{name} tabletop game updated. Game ID = {tabletopGame.IdGame}";
                    TempData[Output.ErrorOccurred] = false;
                }
                catch (Exception exc)
                {
                    System.Diagnostics.Debug.WriteLine("ERROR UPDATING PLAN");
                    TempData[Output.Message] = $"Error while updating tabletop game {tabletopGame.IdGame}";
                    TempData[Output.ErrorOccurred] = true;
                    ModelState.AddModelError(string.Empty, exc.Message);
                }
            }
            else
            {
                TempData[Output.Message] = $"Error while updating tabletop game.";
                TempData[Output.ErrorOccurred] = true;
            }
            return RedirectToAction(nameof(Index), new { page = page, sort = sort, ascending = ascending });
        }

        public async Task<IActionResult> Delete(int id, int page = 1, int sort = 1, bool ascending = true)
        {
            ViewBag.Tables = Output.Tables;
            var tabletopGame = ctx.TabletopGame.Find(id);
            if (tabletopGame != null)
            {
                try
                {
                    ctx.Remove(tabletopGame);
                    await ctx.SaveChangesAsync();

                    TempData[Constants.Message] = $"Successfully deleted tabletop game with ID {tabletopGame.IdGame} ";
                    TempData[Constants.ErrorOccurred] = false;

                }
                catch (Exception e)
                {

                    TempData[Constants.Message] = "Failed deletion of tabletop game" + e.Message;
                    TempData[Constants.ErrorOccurred] = true;
                }
            }
            else
            {
                TempData[Constants.Message] = $"There is no tapletop game with ID: {id}";
                TempData[Constants.ErrorOccurred] = true;
            }
            return RedirectToAction(nameof(Index), new { page, sort, ascending });

        }

        private async Task PrepareAdditionalValues()
        {
            var domains = await ctx.GameDomain
                                  .OrderBy(p => p.IdDomain)
                                  .Select(p => new { p.IdDomain, p.NameDomain })
                                  .ToListAsync();
            var mechanics = await ctx.GameMechanic
                                  .OrderBy(p => p.IdMechanic)
                                  .Select(p => new { p.IdMechanic, p.NameMechanic })
                                  .ToListAsync();
            ViewBag.Domains = new SelectList(domains, nameof(GameDomain.IdDomain), nameof(GameDomain.NameDomain));
            ViewBag.Mechanics = new SelectList(mechanics, nameof(GameMechanic.IdMechanic), nameof(GameMechanic.NameMechanic));
        }

        public async Task<IActionResult> Download()
        {
            ViewBag.Tables = Output.Tables;

            var query = ctx.TabletopGame.AsNoTracking();

            query = query.ApplySort(1, true);

            var tabletopGames = await query
                .Select(p => new TabletopGameViewModel
                {
                    IdGame = p.IdGame,
                    NameGame = p.NameGame,
                    YearGame = p.YearGame,
                    MinPlayers = p.MinPlayers,
                    MaxPlayers = p.MaxPlayers,
                    AverageRating = p.AverageRating,
                    AverageComplexity = p.AverageComplexity,
                    PlayTime = p.PlayTime
                })
                .ToListAsync();

            var pagingInfo = new PagingInfo
            {
                CurrentPage = 1,
                Ascending = true,
                Sort = 1
            };

            var model = new TabletopGamesViewModel
            {
                TabletopGames = tabletopGames,
                PagingInfo = pagingInfo
            };

            GetFile(model, 1);
            GetFile(model, 0);

            return View();
        }

        private string GenerateJSON(TabletopGamesViewModel model, bool single)
        {
            StringBuilder fileContent = new StringBuilder();
            if (!single)
            {
                fileContent.Append("[");
            }

            foreach (var obj in model.TabletopGames)
            {
                fileContent.Append("{\"@context\": {");
                fileContent.Append("\"@vocab\": \"https://schema.org/\",");
                fileContent.Append("\"NameGame\": \"name\",");
                fileContent.Append("\"MinPlayer\": \"minValue\",");
                fileContent.Append("\"MaxPlayer\": \"maxValue\",");
                fileContent.Append("\"AverageRating\": \"ratingValue\"},");

                fileContent.Append("\"IdGame\": " + obj.IdGame + ",");
                fileContent.Append("\"NameGame\": \"" + obj.NameGame + "\",");
                fileContent.Append("\"YearGame\": " + obj.YearGame + ",");
                fileContent.Append("\"MinPlayer\": " + obj.MinPlayers + ",");
                fileContent.Append("\"MaxPlayer\": " + obj.MaxPlayers + ",");
                fileContent.Append("\"AverageRating\": " + obj.AverageRating + ",");
                fileContent.Append("\"AverageComplexity\": " + obj.AverageComplexity + ",");
                fileContent.Append("\"PlayTime\": " + obj.PlayTime + "},");
            }

            fileContent.Remove(fileContent.Length - 1, 1);

            if (!single)
            {
                fileContent.Append("]");
            }

            return fileContent.ToString();
        }

        private string GenerateSoloJSON(TabletopGameViewModel model)
        {
            StringBuilder fileContent = new StringBuilder();

            fileContent.Append("{\"@context\": {");
            fileContent.Append("\"@vocab\": \"https://schema.org/\",");
            fileContent.Append("\"NameGame\": \"name\",");
            fileContent.Append("\"MinPlayer\": \"minValue\",");
            fileContent.Append("\"MaxPlayer\": \"maxValue\",");
            fileContent.Append("\"AverageRating\": \"ratingValue\"},");

            fileContent.Append("\"IdGame\": " + model.IdGame + ",");
            fileContent.Append("\"NameGame\": \"" + model.NameGame + "\",");
            fileContent.Append("\"YearGame\": " + model.YearGame + ",");
            fileContent.Append("\"MinPlayer\": " + model.MinPlayers + ",");
            fileContent.Append("\"MaxPlayer\": " + model.MaxPlayers + ",");
            fileContent.Append("\"AverageRating\": " + model.AverageRating + ",");
            fileContent.Append("\"AverageComplexity\": " + model.AverageComplexity + ",");
            fileContent.Append("\"PlayTime\": " + model.PlayTime + "}");

            return fileContent.ToString();
        }

        private string GenerateCSV(TabletopGamesViewModel model)
        {
            StringBuilder fileContent = new StringBuilder("IdGame,NameGame,YearGame,MinPlayers,MaxPlayers,AverageRating,AverageComplexity,PlayTime\n");
            foreach (var obj in model.TabletopGames)
            {
                string line = "";
                line += obj.IdGame + "," +
                               obj.NameGame + "," +
                               obj.YearGame + "," +
                               obj.MinPlayers + "," +
                               obj.MaxPlayers + "," +
                               obj.AverageRating + "," +
                               obj.AverageComplexity + "," +
                               obj.PlayTime + "\n";

                fileContent.Append(line);
            }

            return fileContent.ToString();
        }

        [HttpGet]
        private void GetFile(TabletopGamesViewModel model, int file)
        {
            var logPath = @"bin\tabletopgames.json";
            var logFile = System.IO.File.Create(logPath);
            var logWriter = new StreamWriter(logFile);
            logWriter.Write(GenerateJSON(model, false));
            logWriter.Dispose();

            logPath = @"bin\tabletopgames.csv";
            logFile = System.IO.File.Create(logPath);
            logWriter = new StreamWriter(logFile);
            logWriter.Write(GenerateCSV(model));
            logWriter.Dispose();
        }

        private void GetSoloFile(TabletopGameViewModel model)
        {
            var logPath = @"bin\tabletopgame.json";
            var logFile = System.IO.File.Create(logPath);
            var logWriter = new StreamWriter(logFile);
            logWriter.Write(GenerateSoloJSON(model));
            logWriter.Dispose();
        }

        [HttpGet]
        public async Task<IActionResult> DownloadOne(int id)
        {
            ViewBag.Tables = Output.Tables;

            var model = await ctx.TabletopGame
                                  .Select(p => new TabletopGameViewModel
                                  {
                                      IdGame = p.IdGame,
                                      NameGame = p.NameGame,
                                      YearGame = p.YearGame,
                                      MinPlayers = p.MinPlayers,
                                      MaxPlayers = p.MaxPlayers,
                                      AverageRating = p.AverageRating,
                                      AverageComplexity = p.AverageComplexity,
                                      PlayTime = p.PlayTime
                                  })
                                  .AsNoTracking()
                                  .Where(o => o.IdGame == id)
                                  .SingleOrDefaultAsync();

            GetSoloFile(model);

            return View(model);
        }
    }
}
