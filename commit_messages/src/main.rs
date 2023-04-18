use chrono::{TimeZone, Utc};
use git2::Repository;
use std::fs::{self, OpenOptions};
use std::io::{BufWriter, Write};
use std::path::Path;

fn main() {
    let repo = Repository::open("..").unwrap();
    let output_dir = "./output";
    fs::create_dir_all(output_dir).unwrap();

    let mut revwalk = repo.revwalk().unwrap();
    revwalk.push_head().unwrap();
    revwalk.set_sorting(git2::Sort::TIME).unwrap();

    for id in revwalk {
        let commit = repo.find_commit(id.unwrap()).unwrap();
        let hash = commit.id().to_string();
        let date = commit.time();
        let date_utc = Utc.timestamp_opt(date.seconds(), 0).unwrap();
        let date_formatted = date_utc.format("%Y-%m-%d").to_string();
        let year = date_utc.format("%Y").to_string();
        let year_output_dir = format!("{output_dir}/{year}");

        fs::create_dir_all(&year_output_dir).unwrap();

        let file_name = format!("{year_output_dir}/{date_formatted}-{hash}.md");

        if !Path::new(&file_name).exists() {
            let message = commit
                .message()
                .unwrap_or("")
                .trim()
                .trim_matches('"')
                .to_string();

            let file = OpenOptions::new()
                .write(true)
                .create_new(true)
                .open(file_name)
                .unwrap();
            let mut writer = BufWriter::new(file);
            writer.write_all(message.as_bytes()).unwrap();
        }
    }
}
