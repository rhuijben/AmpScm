# AmpScm - Amplifying your Git Source Code Management
[![CI](https://github.com/AmpScm/AmpScm/actions/workflows/msbuild.yml/badge.svg)](https://github.com/AmpScm/AmpScm/actions/workflows/msbuild.yml)

This project provides a few layers of tools that allow accessing your repository from .Net without external dependencies. Unlike the libGit2 apis the code is completely managed, 100% Apache 2 Licensed, and uses many optional modern Git optimizations (E.g. commit chains, bitmap indexes, etc.) to improve performance over just scanning the repository.

## AmpScm.Buckets
[![latest version](https://img.shields.io/nuget/v/AmpScm.Buckets)](https://www.nuget.org/packages/AmpScm.Buckets)

This library provides zero-copy stream layering over different datasources, modeled like the *Apache Serf* buckets, but then completely .Net based and async enabled. When you read a bit of data the least amount of work necessary is done up the tree, and only if the highest upper layer reports a delay the task is waiting.

## AmpScm.Git.Repository
[![latest version](https://img.shields.io/nuget/v/AmpScm.Git.Repository)](https://www.nuget.org/packages/AmpScm.Git.Repository)

Completely managed Git repository level library, providing access to the repository as both *IQueryable<>* and *IAsyncEnumerable<>* and even custom *IAsyncQueryable<>* support, to allow extending the repository walk algorithm dynamically.
  
Soon walking history should be as easy as something like:
  
```cs
using AmpScm.Git;
    
using (var repo = await GitRepository.OpenAsync(Environment.CurrentDirectory))
{
    await foreach (var r in repo.Head.Revisions)
    {
        Console.WriteLine($"commit {r.Commit.Id}");
        Console.WriteLine($"Author: {r.Commit.Author?.Name} <{r.Commit.Author?.Email}>");
        Console.WriteLine($"Date:   {r.Commit.Author?.When}");
        Console.WriteLine("");
        Console.WriteLine(r.Commit.Message?.TrimEnd() + "\n");
    }
}
```
 
Of course you can also use the non async api if needed. This repository layer is built on top of *Amp.Buckets* via *AmpScm.Buckets.Git*, which could
be used separately. The IAsyncQueryable<T> support is abstracted via the hopefully temporary *AmpScm.Linq.AsyncQueryable*, until Async LINQ is fully
supported in .NET itself.
  
Currently this library is mostly read-only, but writing simple database entities (blob, commit, tree, tag) to the object store is supported.
  
## AmpScm.Git.Client
[![latest version](https://img.shields.io/nuget/v/AmpScm.Git.Client)](https://www.nuget.org/packages/AmpScm.Git.Client)
  
Built on top of the git repository is an early release quick and dirty Git client layer, which forwards operations to the git plumbing code. Mostly
intended for testing the lower layers, but probly useful for more users. May become a more advanced client later on.
