using AutoMapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VotingApp.Data;
using VotingApp.Models;

namespace VotingApp.Automapper
{
    public class MappingProfile:Profile
    {
        public MappingProfile()
        {
            this.CreateMap<Candidate, EditCandidateModel>().ReverseMap();
            this.CreateMap<SaveCandidateVote, Vote>();
        }
    }   
}
