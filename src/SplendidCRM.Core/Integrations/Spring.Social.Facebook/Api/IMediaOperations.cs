#nullable disable
using System;
using System.IO;
using System.Collections.Generic;
namespace Spring.Social.Facebook.Api
{
    public interface IMediaOperations
    {
        List<Album> GetAlbums();
        List<Album> GetAlbums(int offset, int limit);
        List<Album> GetAlbums(string ownerId);
        List<Album> GetAlbums(string ownerId, int offset, int limit);
        Album GetAlbum(string albumId);
        string CreateAlbum(string name, string description);
        byte[] GetAlbumImage(string albumId);
        byte[] GetAlbumImage(string albumId, ImageType imageType);
        List<Photo> GetPhotos();
        List<Photo> GetPhotos(string objectId);
        List<Photo> GetPhotos(string objectId, int offset, int limit);
        Photo GetPhoto(string photoId);
        byte[] GetPhotoImage(string photoId);
        byte[] GetPhotoImage(string photoId, ImageType imageType);
        string PostPhoto(Resource photo);
        string PostPhoto(Resource photo, string caption);
        string PostPhoto(string albumId, Resource photo);
        string PostPhoto(string albumId, Resource photo, string caption);
        List<Video> GetVideos();
        List<Video> GetVideos(int offset, int limit);
        List<Video> GetVideos(string userId);
        List<Video> GetVideos(string userId, int offset, int limit);
        Video GetVideo(string videoId);
        byte[] GetVideoImage(string videoId);
        byte[] GetVideoImage(string videoId, ImageType imageType);
        string PostVideo(Resource video);
        string PostVideo(Resource video, string title, string description);
    }
}
