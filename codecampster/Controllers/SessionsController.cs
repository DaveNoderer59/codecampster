using System.Linq;
using codecampster.Models;
using codecampster.Services;
using Microsoft.Extensions.Logging;
using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using codecampster.ViewModels.Session;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;

namespace codecampster.Controllers
{
    public class SessionsController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IEmailSender _emailSender;
        private readonly ISmsSender _smsSender;
        private readonly ILogger _logger;
        private ApplicationDbContext _context;

        public SessionsController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IEmailSender emailSender,
            ISmsSender smsSender,
            ILoggerFactory loggerFactory,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailSender = emailSender;
            _smsSender = smsSender;
            _logger = loggerFactory.CreateLogger<SpeakersController>();
            _context = context;
        }


        //[ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Client)]
        public IActionResult Agenda()
        {
            ViewBag.Timeslots = _context.Timeslots.OrderBy(t => t.Rank).ToList();
            ViewBag.Tracks = _context.Tracks.ToList();
            ViewBag.TrackCount = ViewBag.Tracks.Count;
            ViewBag.MySessions = new List<int>();
            if (User.Identity.IsAuthenticated)
            {
                ViewBag.MySessions = _context.AttendeeSessions.Where(a => a.AppUser.UserName == User.Identity.Name).Select(a => a.SessionID).ToList();
            }
            IQueryable<Session> sessions = _context.Sessions.Include(s => s.Speaker).Include(s => s.Track).Include(s => s.Timeslot).OrderBy(x => Guid.NewGuid());
            return View(sessions.ToList());
        }

        [Authorize]
        public JsonResult AddSessionToAgenda(int id)
        {
            var user = _context.ApplicationUsers.Where(u => u.UserName == User.Identity.Name).FirstOrDefault();
            if (user != null)
            {
                AttendeeSession obj = new AttendeeSession() { SessionID = id, ApplicationUserId = user.Id };
                _context.AttendeeSessions.Add(obj);
                return Json(_context.SaveChanges() > 0);
            }
            else
            {
                return Json(false);
            }
        }

        [Authorize]
        public JsonResult RemoveSessionFromAgenda(int id)
        {
            var user = _context.ApplicationUsers.Where(u => u.UserName == User.Identity.Name).FirstOrDefault();
            if (user != null)
            {
                AttendeeSession obj = _context.AttendeeSessions.
                    Where(a => a.SessionID == id && a.ApplicationUserId == user.Id).FirstOrDefault();
                if (obj == null)
                    return Json(false);
                _context.AttendeeSessions.Remove(obj);
                return Json(_context.SaveChanges() > 0);
            }
            else
            {
                return Json(false);
            }
        }
        [ResponseCache(Duration = 300,Location=ResponseCacheLocation.Client)]
        public IActionResult Index(string track, string timeslot)
        {
            ViewBag.Timeslots = _context.Timeslots.Where(s=> (!(s.Special == true))).OrderBy(t => t.Rank);
            ViewBag.Tracks = _context.Tracks.OrderBy(x => x.Name);
            IQueryable<Session> sessions = _context.Sessions.Where(s => s.IsApproved && (!(s.Special == true))).Include(s => s.Speaker).Include(s => s.Track).Include(s => s.Timeslot).OrderBy(x => Guid.NewGuid());
            ViewData["Title"] = string.Format("All {0} Sessions",sessions.Count());
            if (!string.IsNullOrEmpty(track))
            {
                int trackID = 0;
                if (int.TryParse(track, out trackID))
                {
                    var tr = _context.Tracks.Single(t => t.ID == trackID);
                    ViewData["Title"] = string.Format("{0} Track Sessions {1}", tr.Name, tr.RoomNumber);
                    return View(sessions.Where(s => (s.TrackID == trackID) && (!(s.Special == true))).OrderBy(t=>t.Timeslot.Rank).ToList());
                }
            }
            if (!string.IsNullOrEmpty(timeslot))
            {
                int timeslotId = 0;
                if (int.TryParse(timeslot, out timeslotId))
                {
                    var ts = _context.Timeslots.Single(t => t.ID == timeslotId);
                    ViewData["Title"] = string.Format("{0} - {1} Sessions", ts.StartTime, ts.EndTime);
                    return View(sessions.Where(s => s.TimeslotID == timeslotId).OrderBy(t=>t.Track.Name).ToList());
                }
            }
            return View(sessions.OrderBy(t=>t.Timeslot.Rank).ThenBy(t=>t.Track.Name).ToList());
        }

        [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Client)]
        public IActionResult Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            Session session = _context.Sessions.Include(s => s.Speaker).Include(s => s.Track).Include(s => s.Timeslot).Single(m => m.SessionID == id);
            ViewBag.CoSpeaker = session.CoSpeakerID.HasValue ? _context.Speakers.Find(session.CoSpeakerID.Value) : null;
            if (session == null)
            {
                return NotFound();
            }

            return View(session);
        }

        // GET: Sessions/Create
        [Authorize]
        public IActionResult Create()
        {
            ViewData["SpeakerID"] = new SelectList(_context.Speakers, "ID", "Speaker");
            return View();
        }

        // POST: Sessions/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public IActionResult Create(Session session)
        {
            if (ModelState.IsValid)
            {
                _context.Sessions.Add(session);
                _context.SaveChanges();
                return RedirectToAction("Index");
            }
            ViewData["SpeakerID"] = new SelectList(_context.Speakers, "ID", "Speaker", session.SpeakerID);
            return View(session);
        }

        // GET: Sessions/Edit/5
        [Authorize]
        public IActionResult Edit(int? id)
        {
            SessionViewModel model = new SessionViewModel();
            ViewData["Level"] = new SelectList(GetLevels(), "Key", "Value");
            if (id.HasValue)
            {
                var sessionDetails = _context.Sessions.Include(s=>s.Speaker).Include(s=>s.Speaker.AppUser).Where(s=>s.SessionID == id.Value).FirstOrDefault();
                if (sessionDetails != null)// && sessionDetails.Speaker.AppUser.UserName==
                {

                    model = new SessionViewModel()
                    {
                        Title = sessionDetails.Name,
                        Description = sessionDetails.Description,
                        CoSpeakers = sessionDetails.CoSpeakers,
                        Keywords = sessionDetails.KeyWords,
                        Level = sessionDetails.Level
                    };
                    ViewData["Level"] = new SelectList(GetLevels(), "Key", "Value", sessionDetails.Level);
                }
                else
                {
                    return NotFound();
                }
            }

            return View(model);
        }

        // POST: Sessions/Edit/5
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(SessionViewModel model, int? id)
        {

            if (ModelState.IsValid)
            {
                var speaker = _context.Speakers.Where(s => s.AppUser.Email == User.Identity.Name).FirstOrDefault();
                if (id.HasValue)
                {
                    Session session = _context.Sessions.Find(id.Value);
                    session.Name = model.Title;
                    session.CoSpeakers = model.CoSpeakers;
                    session.Description = model.Description;
                    session.KeyWords = model.Keywords;
                    session.Level = model.Level;
                    _context.Update(session);
                }
                else
                {
                    Session session = new Session();
                    session.Name = model.Title;
                    session.CoSpeakers = model.CoSpeakers;
                    session.Description = model.Description;
                    session.KeyWords = model.Keywords;
                    session.Level = model.Level;
                    session.SpeakerID = speaker.ID;
                    _context.Sessions.Add(session);
                }
                _context.SaveChanges();
                return RedirectToAction("Sessions", "Speakers");
            }
            Dictionary<int, string> levels = new Dictionary<int, string>();
            levels.Add(1, "All skill levels");
            levels.Add(2, "Some prior knowledge needed");
            levels.Add(3, "Deep Dive");
            ViewData["Level"] = new SelectList(GetLevels(), "Key", "Value", model.Level);
            return View(model);
        }

        private Dictionary<int, string> GetLevels()
        {
            Dictionary<int, string> levels = new Dictionary<int, string>();
            levels.Add(1, "All skill levels");
            levels.Add(2, "Some prior knowledge needed");
            levels.Add(3, "Deep Dive");
            return levels;
        }

        // GET: Sessions/Delete/5
        [Authorize]
        [ActionName("Delete")]
        public IActionResult Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            Session session = _context.Sessions.Single(m => m.SessionID == id);
            if (session == null)
            {
                return NotFound();
            }

            return View(session);
        }

        // POST: Sessions/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize]
        public IActionResult DeleteConfirmed(int id)
        {
            Session session = _context.Sessions.Single(m => m.SessionID == id);
            _context.Sessions.Remove(session);
            _context.SaveChanges();
            return RedirectToAction("Sessions", "Speakers");
        }
    }
}
