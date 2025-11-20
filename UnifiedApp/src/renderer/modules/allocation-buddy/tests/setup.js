// Jest setup file
global.console = {
  ...console,
  // Suppress console errors in tests if needed
  // error: jest.fn(),
  // warn: jest.fn(),
};

// Mock localStorage
const localStorageMock = {
  getItem: jest.fn(),
  setItem: jest.fn(),
  removeItem: jest.fn(),
  clear: jest.fn()
};
global.localStorage = localStorageMock;
