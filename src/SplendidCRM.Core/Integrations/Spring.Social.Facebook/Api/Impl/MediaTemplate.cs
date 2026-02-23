#nullable disable
using System;
using System.Net;
using System.Collections.Generic;
using System.Collections.Specialized;
using Spring.Social.Facebook.Api;

namespace Spring.Social.Facebook.Api.Impl
{
    public class MediaTemplate : AbstractFacebookOperations, IMediaOperations
    {
        public MediaTemplate(string applicationNamespace, RestTemplate restTemplate, bool isAuthorized) : base(applicationNamespace, restTemplate, isAuthorized) { }
        public List<Album> GetAlbums() { return GetAlbums("me", 0, 25); }
        public List<Album> GetAlbums(int offset, int limit) { return GetAlbums("me", offset, limit); }
        public List<Album> GetAlbums(string ownerId) { return GetAlbums(ownerId, 0, 25); }
        public List<Album> GetAlbums(string ownerId, int offset, int limit) { requireAuthorization(); return new List<Album>(); }
        public Album GetAlbum(string albumId) { return FetchObject<Album>(albumId); }
        public string CreateAlbum(string name, string description) { return CreateAlbum("me", name, description); }
        public string CreateAlbum(string ownerId, string name, string description) { requireAuthorization(); return string.Empty; }
        public byte[] GetAlbumImage(string albumId) { return GetAlbumImage(albumId, ImageType.SMALL); }
        public byte[] GetAlbumImage(string albumId, ImageType imageType) { return FetchImage(albumId, "picture", imageType); }
        public List<Photo> GetPhotos() { return GetPhotos("me", 0, 25); }
        public List<Photo> GetPhotos(string objectId) { return GetPhotos(objectId, 0, 25); }
        public List<Photo> GetPhotos(string objectId, int offset, int limit) { requireAuthorization(); return new List<Photo>(); }
        public Photo GetPhoto(string photoId) { return FetchObject<Photo>(photoId); }
        public byte[] GetPhotoImage(string photoId) { return GetPhotoImage(photoId, ImageType.NORMAL); }
        public byte[] GetPhotoImage(string photoId, ImageType imageType) { return FetchImage(photoId, "picture", imageType); }
        public string PostPhoto(Resource photo) { return PostPhoto("me", photo, null); }
        public string PostPhoto(Resource photo, string caption) { return PostPhoto("me", photo, caption); }
        public string PostPhoto(string albumId, Resource photo) { return PostPhoto(albumId, photo, null); }
        public string PostPhoto(string albumId, Resource photo, string caption) { requireAuthorization(); return string.Empty; }
        public List<Video> GetVideos() { return GetVideos("me", 0, 25); }
        public List<Video> GetVideos(int offset, int limit) { return GetVideos("me", offset, limit); }
        public List<Video> GetVideos(string userId) { return GetVideos(userId, 0, 25); }
        public List<Video> GetVideos(string userId, int offset, int limit) { requireAuthorization(); return new List<Video>(); }
        public Video GetVideo(string videoId) { return FetchObject<Video>(videoId); }
        public byte[] GetVideoImage(string videoId) { return GetVideoImage(videoId, ImageType.SMALL); }
        public byte[] GetVideoImage(string videoId, ImageType imageType) { return FetchImage(videoId, "picture", imageType); }
        public string PostVideo(Resource video) { return PostVideo(video, null, null); }
        public string PostVideo(Resource video, string title, string description) { requireAuthorization(); return string.Empty; }
    }
}
