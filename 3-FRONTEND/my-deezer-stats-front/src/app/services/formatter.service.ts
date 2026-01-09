import { Injectable } from '@angular/core';
import { Album, Artist, Track, SearchResult } from '../models/dashboard.models';

export interface FormattedItem {
  mainText: string;
  subText: string;
  imageUrl: string;
  identifier: string;
}

@Injectable({
  providedIn: 'root'
})
export class FormatterService {
  
  private defaultImage = 'assets/default-cover.jpg';

  // Formatage pour les listes
  formatForList(item: Album | Artist | Track, type: 'album' | 'artist' | 'track'): FormattedItem {
    switch (type) {
      case 'album':
        return this.formatAlbum(item as Album);
      case 'artist':
        return this.formatArtist(item as Artist);
      case 'track':
        return this.formatTrack(item as Track);
      default:
        return this.formatGeneric(item);
    }
  }

  // Formatage pour la recherche
  formatSearchResult(item: SearchResult): string {
    return item.type === 'album' 
      ? `${item.title} - ${item.artist}`
      : item.artist || '';
  }

  // Formatage de titre complet
  formatFullTitle(item: Album | Artist | Track, type: 'album' | 'artist' | 'track'): string {
    switch (type) {
      case 'track':
        const track = item as Track;
        return `${track.track} - ${track.artist}${track.album ? ' (' + track.album + ')' : ''}`;
      case 'album':
        const album = item as Album;
        return `${album.title}${album.artist ? ' - ' + album.artist : ''}`;
      case 'artist':
        return (item as Artist).artist;
      default:
        return '';
    }
  }

  private formatAlbum(album: Album): FormattedItem {
    return {
      mainText: album.title,
      subText: album.artist || '',
      imageUrl: album.coverUrl || this.defaultImage,
      identifier: album.title && album.artist ? `${album.title}|${album.artist}` : ''
    };
  }

  private formatArtist(artist: Artist): FormattedItem {
    return {
      mainText: artist.artist,
      subText: '',
      imageUrl: artist.coverUrl || this.defaultImage,
      identifier: artist.artist || ''
    };
  }

  private formatTrack(track: Track): FormattedItem {
    return {
      mainText: track.track,
      subText: `${track.artist} - ${track.album || ''}`,
      imageUrl: track.trackUrl || this.defaultImage,
      identifier: ''
    };
  }

  private formatGeneric(item: any): FormattedItem {
    return {
      mainText: '',
      subText: '',
      imageUrl: this.defaultImage,
      identifier: ''
    };
  }

  // Helper pour obtenir l'identifiant de navigation
  getNavigationIdentifier(item: any, type: 'album' | 'artist'): string {
    if (type === 'album') {
      const title = item.title ?? '';
      const artist = item.artist ?? '';
      return title && artist ? `${title}|${artist}` : '';
    } else if (type === 'artist') {
      return item.artist ?? '';
    }
    return '';
  }
}