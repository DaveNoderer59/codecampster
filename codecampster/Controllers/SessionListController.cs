using System;
using System.Linq;
using codecampster.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
//using System.Web.Http;

namespace codecampster.Controllers
{

    [Route("api/[controller]")]
    [Produces("application/json")]
    public class SessionListController : Controller
    {
        private ApplicationDbContext _context;

        public SessionListController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [ResponseCache(Duration =3600,Location =ResponseCacheLocation.Any)]
        public IActionResult Get()
        {
            return Ok(_context.Sessions.Where(s=>s.IsApproved).Include(s => s.Speaker).ToList());
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _context.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
