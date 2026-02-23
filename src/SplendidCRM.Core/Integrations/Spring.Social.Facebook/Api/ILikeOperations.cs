#nullable disable
using System;
using System.IO;
using System.Collections.Generic;
namespace Spring.Social.Facebook.Api
{
    public interface ILikeOperations
    {
        List<Reference> GetLikes(string objectId);
        List<Page> GetPagesLiked();
        List<Page> GetPagesLiked(string userId);
        void Like(string objectId);
        void Unlike(string objectId);
        List<Page> GetBooks();
        List<Page> GetBooks(string userId);
        List<Page> GetMovies();
        List<Page> GetMovies(string userId);
        List<Page> GetMusic();
        List<Page> GetMusic(string userId);
        List<Page> GetTelevision();
        List<Page> GetTelevision(string userId);
        List<Page> GetActivities();
        List<Page> GetActivities(string userId);
        List<Page> GetInterests();
        List<Page> GetInterests(string userId);
        List<Page> GetGames();
        List<Page> GetGames(string userId);
    }
}
