using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace VotingApp.Models
{
    public class EditCandidateModel
    {
        public Guid Id { get; set; }
        [Required]
        public string Name { get; set; }
        public string Description { get; set; }
        [Display(Name = "Short Bio")]
        public string ShortBiography { get; set; }
        public string Avatar { get; set; }
        public IFormFile Image { get; set; }
    }
}
