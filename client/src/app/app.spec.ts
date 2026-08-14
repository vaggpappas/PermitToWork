import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { App } from './app';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      // The shell injects AuthService, which injects HttpClient and Router. Providing both
      // is the whole setup — nothing is mocked, because nothing here talks to the network.
      providers: [provideHttpClient(), provideRouter([])],
    }).compileComponents();
  });

  it('creates the shell', () => {
    const fixture = TestBed.createComponent(App);

    expect(fixture.componentInstance).toBeTruthy();
  });

  it('hides the navigation until somebody signs in', async () => {
    localStorage.removeItem('ptw.accessToken');

    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();

    // No token means no sidebar — the login screen should not offer links the user cannot
    // follow yet.
    expect((fixture.nativeElement as HTMLElement).querySelector('.sidebar')).toBeNull();
  });
});
